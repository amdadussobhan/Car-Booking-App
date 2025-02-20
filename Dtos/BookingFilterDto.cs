namespace Wafi.SampleTest.Dtos
{
    public class BookingFilterDto
    {
        public Guid CarId { get; set; }

        public DateTime StartBookingDate { get; set; }
        
        public DateTime EndBookingDate { get; set; }
    }
}
