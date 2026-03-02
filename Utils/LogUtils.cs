using HeatHarmony.Models;
using System.Threading.Channels;

namespace HeatHarmony.Utils
{
    public static class LogUtils
    {
        public static void AddChangeRecord(List<HarmonyChange> changeList, Provider provider, HarmonyChangeType changeType, string? description = null)
        {
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            changeList.RemoveAll(c => c.Time < threeDaysAgo);

            changeList.Add(new HarmonyChange
            {
                Time = DateTime.Now,
                Provider = provider,
                ChangeType = changeType,
                Description = description
            });
        }
    }
}
