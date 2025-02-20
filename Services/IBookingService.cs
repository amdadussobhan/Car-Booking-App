using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wafi.SampleTest.Dtos;

namespace Wafi.SampleTest.Controllers
{
    public interface IBookingService
    {
        Task<IEnumerable<BookingCalendarDto>> GetCalendarBookings(BookingFilterDto input);
        Task<string> CreateBooking(CreateBookingDto newBooking);
    }
}