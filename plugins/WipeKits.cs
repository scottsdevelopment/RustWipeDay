﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using static API.Commands.Command;

namespace Oxide.Plugins
{
    [Info("Wipe Kits", "TTV OdsScott", "2.0.0")]
    [Description("Puts a configurable cooldown on each kit depending on their kitname.")]
    public class WipeKits : RustPlugin
    {
        #region Declaration

        private static ConfigFile _cFile;
        private DateTime _cachedWipeTime;
        private const string Perm = "wipekits.bypass";

        #endregion

        #region Config

        private class ConfigFile
        {
            [JsonProperty(PropertyName = "Kit Names & Cooldowns - Cooldowns (minutes)")]
            public Dictionary<string, float> Kits;

            [JsonProperty(PropertyName = "Use GUI Kits (true/false)")]
            public bool UseGui { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Kits = new Dictionary<string, float>()
                    {
                        ["kitname1"] = 5,
                        ["kitname2"] = 5
                    },
                    UseGui = false
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading default configuration file...");
            _cFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _cFile = Config.ReadObject<ConfigFile>();
                if (_cFile == null)
                {
                    Regenerate();
                }
            }
            catch { Regenerate(); }
        }

        protected override void SaveConfig() => Config.WriteObject(_cFile);

        private void Regenerate()
        {
            PrintWarning($"Configuration file at 'oxide/config/{Name}.json' seems to be corrupt, regenerating...");
            LoadDefaultConfig();
        }

        #endregion Config

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Time formatting
                ["DayFormat"] = "<color=orange>{0}</color> day and <color=orange>{1}</color> hours",
                ["DaysFormat"] = "<color=orange>{0}</color> days and <color=orange>{1}</color> hours",
                ["HourFormat"] = "<color=orange>{0}</color> hour and <color=orange>{1}</color> minutes",
                ["HoursFormat"] = "<color=orange>{0}</color> hours and <color=orange>{1}</color> minutes",
                ["MinFormat"] = "<color=orange>{0}</color> minute and <color=orange>{1}</color> seconds",
                ["MinsFormat"] = "<color=orange>{0}</color> minutes and <color=orange>{1}</color> seconds",
                ["SecsFormat"] = "<color=orange>{0}</color> seconds",
                // Can't use command
                ["CantUse"] = "The server's just wiped! Try again in {0}",
            }, this);
        }

        #endregion Lang

        #region Methods

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private string GetFormattedTime(double time)
        {
            var timeSpan = TimeSpan.FromSeconds(time);
            if (timeSpan.TotalSeconds < 1)
            {
                return null;
            }

            if (Math.Floor(timeSpan.TotalDays) >= 1)
            {
                return string.Format(timeSpan.Days > 1 ? Lang("DaysFormat", null, timeSpan.Days, timeSpan.Hours) : Lang("DayFormat", null, timeSpan.Days, timeSpan.Hours));
            }

            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
            {
                return string.Format(timeSpan.Hours > 1 ? Lang("HoursFormat", null, timeSpan.Hours, timeSpan.Minutes) : Lang("HourFormat", null, timeSpan.Hours, timeSpan.Minutes));
            }

            if (Math.Floor(timeSpan.TotalSeconds) >= 60)
            {
                return string.Format(timeSpan.Minutes > 1 ? Lang("MinsFormat", null, timeSpan.Minutes, timeSpan.Seconds) : Lang("MinFormat", null, timeSpan.Minutes, timeSpan.Seconds));
            }

            return Lang("SecsFormat", null, timeSpan.Seconds);
        }

        private TimeSpan GetNextKitTime(float cooldown)
        {
            var timeSince = TimeSpan.FromSeconds((DateTime.UtcNow.ToLocalTime() - _cachedWipeTime).TotalSeconds);
            if (timeSince.TotalSeconds > cooldown * 60)
            {
                return TimeSpan.Zero;
            }

            double timeUntil = cooldown * 60 - Math.Round(timeSince.TotalSeconds);
            return TimeSpan.FromSeconds(timeUntil);
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if (!permission.PermissionExists(Perm))
            {
                permission.RegisterPermission(Perm, this);
            }

            Subscribe(nameof(CanRedeemKit));

            _cachedWipeTime = SaveRestore.SaveCreatedTime.ToLocalTime();
        }

        private object CanRedeemKit(BasePlayer player, string kitName)
        {
            Puts(player + "," + kitName);
            if (permission.UserHasPermission(player.UserIDString, Perm))
            {
                return null;
            }

            if (!_cFile.Kits.TryGetValue(kitName, out float kitCooldown))
            {
                return null;
            }

            if (GetNextKitTime(kitCooldown) == TimeSpan.Zero)
            {
                return null;
            }

            PrintToChat(player, Lang("CantUse", player.UserIDString, GetFormattedTime(GetNextKitTime(kitCooldown).TotalSeconds)));
            return false;
        }

        #endregion
    }
}