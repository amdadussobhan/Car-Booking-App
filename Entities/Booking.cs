using System.ComponentModel.DataAnnotations;
using System;

namespace Wafi.SampleTest.Entities
{
    public class Booking
    {
        public Guid Id { get; set; }

        [Required]
        public DateOnly BookingDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        //Enum: DoesNotRepeat, Daily, Weekly
        public RepeatOption RepeatOption { get; set; }

        public DateOnly? EndRepeatDate { get; set; }

        //Enum: None,Sunday,Monday,Tuesday,Wednesday,Thursday,Friday,Saturday
        public DaysOfWeek? DaysToRepeatOn { get; set; }

        public DateTime RequestedOn { get; set; }

        public Guid CarId { get; set; }

        public Car Car { get; set; }
    }

    [Flags]
    public enum DaysOfWeek
    {
        None = 0,
        Sunday = 1,
        Monday = 2,
        Tuesday = 4,
        Wednesday = 8,
        Thursday = 16,
        Friday = 32,
        Saturday = 64
    }

    public enum RepeatOption
    {
        DoesNotRepeat = 1,
        Daily = 2,
        Weekly = 3
    }
}
