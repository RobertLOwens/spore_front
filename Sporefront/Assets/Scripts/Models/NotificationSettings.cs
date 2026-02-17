using UnityEngine;

namespace Sporefront.Models
{
    public static class NotificationSettings
    {
        // In-Game
        private const string CombatAlertsKey = "notification_combat_alerts";
        private const string EnemySightingsKey = "notification_enemy_sightings";
        private const string BuildingUpdatesKey = "notification_building_updates";
        private const string TrainingUpdatesKey = "notification_training_updates";
        private const string ResearchUpdatesKey = "notification_research_updates";
        private const string ResourceAlertsKey = "notification_resource_alerts";

        // Push
        private const string PushEnabledKey = "push_notifications_enabled";
        private const string PushCombatKey = "push_combat_alerts";
        private const string PushEnemyKey = "push_enemy_sightings";
        private const string PushBuildingKey = "push_building_updates";
        private const string PushTrainingKey = "push_training_updates";
        private const string PushResearchKey = "push_research_updates";
        private const string PushResourceKey = "push_resource_alerts";

        private static bool GetBool(string key, bool defaultValue = true)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        // In-Game
        public static bool CombatAlertsEnabled { get => GetBool(CombatAlertsKey); set => SetBool(CombatAlertsKey, value); }
        public static bool EnemySightingsEnabled { get => GetBool(EnemySightingsKey); set => SetBool(EnemySightingsKey, value); }
        public static bool BuildingUpdatesEnabled { get => GetBool(BuildingUpdatesKey); set => SetBool(BuildingUpdatesKey, value); }
        public static bool TrainingUpdatesEnabled { get => GetBool(TrainingUpdatesKey); set => SetBool(TrainingUpdatesKey, value); }
        public static bool ResearchUpdatesEnabled { get => GetBool(ResearchUpdatesKey); set => SetBool(ResearchUpdatesKey, value); }
        public static bool ResourceAlertsEnabled { get => GetBool(ResourceAlertsKey); set => SetBool(ResourceAlertsKey, value); }

        // Push
        public static bool PushNotificationsEnabled { get => GetBool(PushEnabledKey); set => SetBool(PushEnabledKey, value); }
        public static bool PushCombatAlertsEnabled { get => GetBool(PushCombatKey); set => SetBool(PushCombatKey, value); }
        public static bool PushEnemySightingsEnabled { get => GetBool(PushEnemyKey); set => SetBool(PushEnemyKey, value); }
        public static bool PushBuildingUpdatesEnabled { get => GetBool(PushBuildingKey); set => SetBool(PushBuildingKey, value); }
        public static bool PushTrainingUpdatesEnabled { get => GetBool(PushTrainingKey); set => SetBool(PushTrainingKey, value); }
        public static bool PushResearchUpdatesEnabled { get => GetBool(PushResearchKey); set => SetBool(PushResearchKey, value); }
        public static bool PushResourceAlertsEnabled { get => GetBool(PushResourceKey); set => SetBool(PushResourceKey, value); }

        public static bool IsEnabled(GameNotificationType type)
        {
            if (type is ArmyAttackedNotification || type is VillagerAttackedNotification) return CombatAlertsEnabled;
            if (type is ArmySightedNotification) return EnemySightingsEnabled;
            if (type is BuildingCompletedNotification || type is UpgradeCompletedNotification || type is EntrenchmentCompletedNotification) return BuildingUpdatesEnabled;
            if (type is TrainingCompletedNotification) return TrainingUpdatesEnabled;
            if (type is ResearchCompletedNotification) return ResearchUpdatesEnabled;
            if (type is GatheringCompletedNotification || type is ResourcesMaxedNotification || type is ResourcePointDepletedNotification) return ResourceAlertsEnabled;
            return true;
        }

        public static bool IsPushEnabled(GameNotificationType type)
        {
            if (!PushNotificationsEnabled) return false;

            if (type is ArmyAttackedNotification || type is VillagerAttackedNotification) return PushCombatAlertsEnabled;
            if (type is ArmySightedNotification) return PushEnemySightingsEnabled;
            if (type is BuildingCompletedNotification || type is UpgradeCompletedNotification || type is EntrenchmentCompletedNotification) return PushBuildingUpdatesEnabled;
            if (type is TrainingCompletedNotification) return PushTrainingUpdatesEnabled;
            if (type is ResearchCompletedNotification) return PushResearchUpdatesEnabled;
            if (type is GatheringCompletedNotification || type is ResourcesMaxedNotification || type is ResourcePointDepletedNotification) return PushResourceAlertsEnabled;
            return true;
        }

        public static void SetAllPushCategories(bool enabled)
        {
            PushCombatAlertsEnabled = enabled;
            PushEnemySightingsEnabled = enabled;
            PushBuildingUpdatesEnabled = enabled;
            PushTrainingUpdatesEnabled = enabled;
            PushResearchUpdatesEnabled = enabled;
            PushResourceAlertsEnabled = enabled;
        }
    }
}
