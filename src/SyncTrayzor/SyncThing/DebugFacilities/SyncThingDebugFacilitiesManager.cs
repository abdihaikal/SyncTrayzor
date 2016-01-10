﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SyncTrayzor.SyncThing.ApiClient;

namespace SyncTrayzor.SyncThing.DebugFacilities
{
    public interface ISyncThingDebugFacilitiesManager
    {
        bool SupportsRestartlessUpdate { get; }
        IReadOnlyList<DebugFacility> DebugFacilities { get; }

        void SetEnabledDebugFacilities(IEnumerable<string> enabledDebugFacilities);
    }

    public class SyncThingDebugFacilitiesManager : ISyncThingDebugFacilitiesManager
    {
        private static readonly Dictionary<string, string> legacyFacilities = new Dictionary<string, string>()
        {
            { "beacon", "the beacon package" },
            { "discover", "the discover package" },
            { "events", "the events package" },
            { "files", "the files package" },
            { "http", "the main package; HTTP requests" },
            { "locks", "the locks package; trace long held locks" },
            { "net", "the main package; connections & network events" },
            { "model", "the model package" },
            { "scanner", "the scanner package" },
            { "stats", "the stats package" },
            { "suture", "the suture package; service management" },
            { "upnp", "the upnp package" },
            { "xdr", "the xdr package" }
        };

        private readonly SynchronizedTransientWrapper<ISyncThingApiClient> apiClient;

        private DebugFacilitiesSettings fetchedDebugFacilitySettings;
        private List<string> enabledDebugFacilities = new List<string>();

        public bool SupportsRestartlessUpdate { get; private set; }
        public IReadOnlyList<DebugFacility> DebugFacilities { get; private set; }

        public SyncThingDebugFacilitiesManager(SynchronizedTransientWrapper<ISyncThingApiClient> apiClient)
        {
            this.apiClient = apiClient;
            this.SupportsRestartlessUpdate = false;
            this.DebugFacilities = new List<DebugFacility>();
        }

        public async Task LoadAsync(Version syncthingVersion)
        {
            if (syncthingVersion.Minor < 12)
            {
                this.SupportsRestartlessUpdate = false;
                this.fetchedDebugFacilitySettings = null;
            }
            else
            {
                this.SupportsRestartlessUpdate = true;
                this.fetchedDebugFacilitySettings = await this.apiClient.Value.FetchDebugFacilitiesAsync();
            }

            this.UpdateDebugFacilities();
        }

        private void UpdateDebugFacilities()
        {
            if (this.SupportsRestartlessUpdate)
                this.DebugFacilities = this.fetchedDebugFacilitySettings.Facilities.Select(kvp => new DebugFacility(kvp.Key, kvp.Value, this.enabledDebugFacilities.Contains(kvp.Key))).ToList().AsReadOnly();
            else
                this.DebugFacilities = legacyFacilities.Select(kvp => new DebugFacility(kvp.Key, kvp.Value, this.enabledDebugFacilities.Contains(kvp.Key))).ToList().AsReadOnly();
        }

        public async void SetEnabledDebugFacilities(IEnumerable<string> enabledDebugFacilities)
        {
            var enabledDebugFacilitiesList = enabledDebugFacilities?.ToList() ?? new List<string>();

            if (new HashSet<string>(this.enabledDebugFacilities).SetEquals(enabledDebugFacilitiesList))
                return;

            this.enabledDebugFacilities = enabledDebugFacilitiesList;
            this.UpdateDebugFacilities();

            if (!this.SupportsRestartlessUpdate)
                return;

            var enabled = this.DebugFacilities.Where(x => x.IsEnabled).Select(x => x.Name).ToList();
            var disabled = this.DebugFacilities.Where(x => !x.IsEnabled).Select(x => x.Name).ToList();

            var apiClient = this.apiClient.Value;
            if (apiClient != null)
                await apiClient.SetDebugFacilitiesAsync(enabled, disabled);
        }
    }
}