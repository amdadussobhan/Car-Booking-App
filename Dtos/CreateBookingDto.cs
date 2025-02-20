using System.ComponentModel.DataAnnotations;
using Wafi.SampleTest.Entities;

namespace Wafi.SampleTest.Dtos
{
    public class CreateBookingDto
    {
        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        public RepeatOption RepeatOption { get; set; }

        public DateTime? EndRepeatDate { get; set; }

        public DaysOfWeek? DaysToRepeatOn { get; set; }

        public DateTime RequestedOn { get; set; }

        public Guid CarId { get; set; }
    }
}
