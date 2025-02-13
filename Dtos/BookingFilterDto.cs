namespace Wafi.SampleTest.Dtos
{
    public class BookingFilterDto
    {
        public Guid CarId { get; set; }
        public DateOnly StartBookingDate { get; set; }
        public DateOnly EndBookingDate { get; set; }
    }
}
