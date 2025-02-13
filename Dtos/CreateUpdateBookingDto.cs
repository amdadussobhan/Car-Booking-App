using System.ComponentModel.DataAnnotations;
using Wafi.SampleTest.Entities;

namespace Wafi.SampleTest.Dtos
{
    public class CreateUpdateBookingDto
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
    }
}
