
namespace ServiceSiteScheduling.Tasks
{
    interface IFixedSchedule
    {
        Utilities.Time ScheduledTime { get; set; }
    }
}
