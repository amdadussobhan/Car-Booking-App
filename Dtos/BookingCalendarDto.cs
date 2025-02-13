using System.ComponentModel.DataAnnotations;

namespace Wafi.SampleTest.Dtos
{
    public class BookingCalendarDto
    {
        public DateOnly BookingDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }
        public string CarModel { get; set; }
    }
}
