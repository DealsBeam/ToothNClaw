using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace Tooth.Backend
{
    public static class MotherboardInfo
    {
        // --- Settings keys / prefix
        private const string SettingsPrefix = "MOTHERBOARD-";
        private const string SettingsBootKey = SettingsPrefix + "BootSession";

        // --- WMI searchers (same classes you used)
        private static readonly ManagementObjectSearcher baseboardSearcher =
            new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");

        private static readonly ManagementObjectSearcher motherboardSearcher =
            new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");

        private static readonly ManagementObjectSearcher processorSearcher =
            new("root\\CIMV2", "SELECT * FROM Win32_Processor");

        private static readonly ManagementObjectSearcher computerSearcher =
            new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem");

        // lock for refresh / collect
        private static readonly object refreshLock = new();

        // List of fields we populate per WMI class (used when collecting)
        // Keys are the same shape your original code used: "<collection>-<Property>"
        private static readonly Dictionary<string, string[]> fieldsToCollect = new()
        {
            // baseboard (Win32_BaseBoard)
            { "baseboard", new[] {
                "Manufacturer","Model","PartNumber","Product","Removable","Replaceable",
                "SerialNumber","Version","InstallDate","HostingBoard","Status"
            } },

            // motherboard (Win32_MotherboardDevice)
            { "motherboard", new[] {
                "Availability","PNPDeviceID","PrimaryBusType","RevisionNumber","SecondaryBusType",
                "SystemName"
            } },

            // processor (Win32_Processor)
            { "processor", new[] {
                "ProcessorId","Name","Manufacturer","NumberOfCores","MaxClockSpeed"
            } },

            // computer (Win32_ComputerSystem)
            { "computer", new[] { "Model" } }
        };

        // --- Public properties (preserve original API) ---
        public static string Availability => ConvertToAvailabilityString(Convert.ToString(queryCacheValue("motherboard", "Availability")));
        public static bool HostingBoard => Convert.ToBoolean(queryCacheValue("baseboard", "HostingBoard"));
        public static string InstallDate
        {
            get
            {
                var raw = Convert.ToString(queryCacheValue("baseboard", "InstallDate"));
                if (!string.IsNullOrEmpty(raw))
                    return ConvertToDateTime(raw);
                return string.Empty;
            }
        }

        public static string Manufacturer => Convert.ToString(queryCacheValue("baseboard", "Manufacturer")) ?? string.Empty;
        public static string Model => Convert.ToString(queryCacheValue("baseboard", "Model")) ?? string.Empty;
        public static string SystemModel => Convert.ToString(queryCacheValue("computer", "Model")) ?? string.Empty;
        public static int NumberOfCores => Convert.ToInt32(queryCacheValue("processor", "NumberOfCores"));
        public static string PartNumber => Convert.ToString(queryCacheValue("baseboard", "PartNumber")) ?? string.Empty;
        public static string PNPDeviceID => Convert.ToString(queryCacheValue("motherboard", "PNPDeviceID")) ?? string.Empty;
        public static string PrimaryBusType => Convert.ToString(queryCacheValue("motherboard", "PrimaryBusType")) ?? string.Empty;
        public static string ProcessorID => (Convert.ToString(queryCacheValue("processor", "ProcessorId")) ?? string.Empty).TrimEnd();
        public static string ProcessorName => (Convert.ToString(queryCacheValue("processor", "Name")) ?? string.Empty).TrimEnd();
        public static string ProcessorManufacturer => (Convert.ToString(queryCacheValue("processor", "Manufacturer")) ?? string.Empty).TrimEnd();
        public static uint ProcessorMaxClockSpeed => ConvertToUInt32(queryCacheValue("processor", "MaxClockSpeed"));
        public static uint ProcessorMaxPCoreSpeed
        {
            get
            {
                string model = MotherboardInfo.Product;

                return model switch
                {
                    "MS-1T41" => 5100,       // MHz
                    "MS-1T42" => 5100,       // MHz
                    "MS-1T52" => 5100,       // MHz
                    "Claw A8" => 64000,       // example, adjust if needed
                    _ => 64000       // fallback default
                };
            }
        }
        public static uint ProcessorMaxECoreSpeed
        {
            get
            {
                string model = MotherboardInfo.Product;

                return model switch
                {
                    "MS-1T41" => 3800,       // MHz
                    "MS-1T42" => 3800,       // MHz
                    "MS-1T52" => 3800,       // MHz

                    "Claw A8" => 64000,       // example, adjust if needed
                    _ => 64000       // fallback default
                };
            }
        }
        public static string Product => Convert.ToString(queryCacheValue("baseboard", "Product")) ?? string.Empty;
        public static bool Removable => Convert.ToBoolean(queryCacheValue("baseboard", "Removable"));
        public static bool Replaceable => Convert.ToBoolean(queryCacheValue("baseboard", "Replaceable"));
        public static string RevisionNumber => Convert.ToString(queryCacheValue("motherboard", "RevisionNumber")) ?? string.Empty;
        public static string SecondaryBusType => Convert.ToString(queryCacheValue("motherboard", "SecondaryBusType")) ?? string.Empty;
        public static string SerialNumber => Convert.ToString(queryCacheValue("baseboard", "SerialNumber")) ?? string.Empty;
        public static string Status => Convert.ToString(queryCacheValue("baseboard", "Status")) ?? string.Empty;
        public static string SystemName => Convert.ToString(queryCacheValue("motherboard", "SystemName")) ?? string.Empty;
        public static string Version => Convert.ToString(queryCacheValue("baseboard", "Version")) ?? string.Empty;

        // --- Public helpers you had originally (keep them) ---
        private static string ConvertToDateTime(string unconvertedTime)
        {
            try
            {
                var year = int.Parse(unconvertedTime.Substring(0, 4));
                var month = int.Parse(unconvertedTime.Substring(4, 2));
                var date = int.Parse(unconvertedTime.Substring(6, 2));
                var hours = int.Parse(unconvertedTime.Substring(8, 2));
                var minutes = int.Parse(unconvertedTime.Substring(10, 2));
                var seconds = int.Parse(unconvertedTime.Substring(12, 2));
                var meridian = "AM";
                if (hours > 12)
                {
                    hours -= 12;
                    meridian = "PM";
                }

                return $"{date}/{month}/{year} {hours}:{minutes}:{seconds} {meridian}";
            }
            catch
            {
                return unconvertedTime;
            }
        }

        private static string GetAvailability(int availability)
        {
            // keep original mapping
            switch (availability)
            {
                case 1: return "Other";
                case 2: return "Unknown";
                case 3: return "Running or Full Power";
                case 4: return "Warning";
                case 5: return "In Test";
                case 6: return "Not Applicable";
                case 7: return "Power Off";
                case 8: return "Off Line";
                case 9: return "Off Duty";
                case 10: return "Degraded";
                case 11: return "Not Installed";
                case 12: return "Install Error";
                case 13: return "Power Save - Unknown";
                case 14: return "Power Save - Low Power Mode";
                case 15: return "Power Save - Standby";
                case 16: return "Power Cycle";
                case 17: return "Power Save - Warning";
                default: return "Unknown";
            }
        }

        // --- Internal helpers ---

        /// <summary>
        /// Convert the raw setting content into the "Availability" text.
        /// This accepts strings or ints as stored.
        /// </summary>
        private static string ConvertToAvailabilityString(string raw)
        {
            if (int.TryParse(raw, out var v))
                return GetAvailability(v);
            return raw ?? string.Empty;
        }

        private static uint ConvertToUInt32(object? o)
        {
            if (o == null) return 0;
            try
            {
                return Convert.ToUInt32(o);
            }
            catch
            {
                // try parse if string
                if (uint.TryParse(Convert.ToString(o), out var v))
                    return v;
                return 0;
            }
        }

        /// <summary>
        /// Central method that returns the cached value (object) for a collection+property.
        /// It ensures the per-boot cache is valid and will trigger a full refresh (one WMI sweep)
        /// if there is no cached entry for the requested key or if the boot session has changed.
        /// </summary>
        private static object? queryCacheValue(string collectionName, string property)
        {
            // Ensure cache is valid for this boot
            EnsureCacheValid();

            string key = SettingsPrefix + collectionName + "-" + property;

            // Try to get the stored value from SettingsManager
            var stored = SettingsManager.Get<object>(key);

            // stored can be null, a primitive, or a List<string>
            return stored;
        }


        private static void EnsureCacheValid()
        {
            // check if we already have any cache
            var anyKey = SettingsPrefix + "baseboard-Product";
            var cached = SettingsManager.Get<object>(anyKey);

            if (cached != null)
            {
                // cache exists, do nothing
                return;
            }

            lock (refreshLock)
            {
                // double-check inside lock
                cached = SettingsManager.Get<object>(anyKey);
                if (cached == null)
                {
                    // first run, collect and cache
                    RefreshCache();
                }
            }
        }

        private static void RefreshCache()
        {
            // Baseboard
            CollectFromSearcher(baseboardSearcher, "baseboard", fieldsToCollect["baseboard"], firstOnly: true);

            // MotherboardDevice
            CollectFromSearcher(motherboardSearcher, "motherboard", fieldsToCollect["motherboard"], firstOnly: true);

            // Processor
            CollectFromSearcher(processorSearcher, "processor", fieldsToCollect["processor"], firstOnly: true);

            // Computer system
            CollectFromSearcher(computerSearcher, "computer", fieldsToCollect["computer"], firstOnly: true);

            Console.WriteLine("[MotherboardInfo] Initial cache populated");
        }

        /// <summary>
        /// Helper to perform queries using a given searcher and store results in SettingsManager.
        /// firstOnly: if true - pick first non-null value per property; if false - gather list across objects.
        /// </summary>
        private static void CollectFromSearcher(ManagementObjectSearcher searcher, string collectionName, string[] properties, bool firstOnly)
        {
            // Perform query
            try
            {
                using var results = searcher.Get();

                if (firstOnly)
                {
                    // For each property, pick first available non-null value among returned ManagementObjects
                    foreach (var prop in properties)
                    {
                        object? val = null;
                        foreach (ManagementObject mo in results)
                        {
                            try
                            {
                                // Some WMI classes use slightly different property names (e.g. ProcessorId vs processorID)
                                if (mo.Properties.Cast<PropertyData>().Any(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)))
                                {
                                    val = mo[prop];
                                }
                                else
                                {
                                    // try case-insensitive lookup fallback
                                    var pd = mo.Properties.Cast<PropertyData>().FirstOrDefault(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase));
                                    if (pd != null)
                                        val = mo[pd.Name];
                                }
                            }
                            catch { /* ignore per-object errors */ }

                            if (val != null) break;
                        }

                        // Store parsed type where appropriate
                        string key = SettingsPrefix + collectionName + "-" + prop;

                        if (val == null)
                        {
                            // remove or set null - SettingsManager.Set will overwrite
                            SettingsManager.Set<object>(key, null!);
                        }
                        else
                        {
                            // try to convert numeric types to numeric CLR types where possible
                            if (int.TryParse(Convert.ToString(val), out var i))
                                SettingsManager.Set<int>(key, i);
                            else if (long.TryParse(Convert.ToString(val), out var l))
                                SettingsManager.Set<long>(key, l);
                            else if (uint.TryParse(Convert.ToString(val), out var ui))
                                SettingsManager.Set<uint>(key, ui);
                            else if (double.TryParse(Convert.ToString(val), out var d))
                                SettingsManager.Set<double>(key, d);
                            else if (bool.TryParse(Convert.ToString(val), out var b))
                                SettingsManager.Set<bool>(key, b);
                            else
                                SettingsManager.Set<string>(key, Convert.ToString(val) ?? string.Empty);
                        }
                    }
                }
                else
                {
                    // Collect lists across all returned ManagementObjects
                    foreach (var prop in properties)
                    {
                        var list = new List<string>();
                        foreach (ManagementObject mo in results)
                        {
                            try
                            {
                                object? val = null;
                                if (mo.Properties.Cast<PropertyData>().Any(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)))
                                {
                                    val = mo[prop];
                                }
                                else
                                {
                                    var pd = mo.Properties.Cast<PropertyData>().FirstOrDefault(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase));
                                    if (pd != null)
                                        val = mo[pd.Name];
                                }

                                if (val != null)
                                {
                                    var s = Convert.ToString(val);
                                    if (!string.IsNullOrEmpty(s))
                                        list.Add(s);
                                }
                            }
                            catch { /* ignore per-object errors */ }
                        }

                        string key = SettingsPrefix + collectionName + "-" + prop;
                        SettingsManager.Set<List<string>>(key, list);
                    }
                }
            }
            catch (Exception ex)
            {
                // on error, ensure we still write nulls for requested props to avoid repeated attempts
                foreach (var prop in properties)
                {
                    string key = SettingsPrefix + collectionName + "-" + prop;
                    try { SettingsManager.Set<object>(key, null!); } catch { }
                }
                Console.WriteLine($"[MotherboardInfo] CollectFromSearcher({collectionName}) error: {ex.Message}");
            }
        }
    }
}
