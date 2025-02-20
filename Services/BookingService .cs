using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wafi.SampleTest.Dtos;
using Wafi.SampleTest.Entities;

namespace Wafi.SampleTest.Controllers
{
    public class BookingService : IBookingService
    {
        private readonly WafiDbContext _context;

        public BookingService(WafiDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BookingCalendarDto>> GetCalendarBookings(BookingFilterDto input)
        {
            // Retriving booking data from database
            var bookingsQuery = _context.Bookings
                .Include(b => b.Car) // include car details
                .AsQueryable();

            // Applying CardId filters if provided
            if (input.CarId != Guid.Empty)
                bookingsQuery = bookingsQuery.Where(b => b.CarId == input.CarId);

            // Applying StartBookingDate filters if provided
            if (input.StartBookingDate != DateTime.MinValue.Date)
            {
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= DateOnly.FromDateTime(input.StartBookingDate));
            }

            // Applying EndBookingDate filters if provided
            if (input.EndBookingDate != DateTime.MinValue.Date)
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate <= DateOnly.FromDateTime(input.EndBookingDate));

            var bookingsList = await bookingsQuery.ToListAsync();

            // Generate calendar bookings
            var calendarBookings = new List<BookingCalendarDto>();

            foreach (var booking in bookingsList)
            {
                var currentDate = booking.BookingDate;
                while (currentDate <= (booking.EndRepeatDate ?? booking.BookingDate))
                {
                    // Checking if the current date matches the recurrence pattern
                    if (IsDayOfWeekMatch(currentDate, (int)(booking.DaysToRepeatOn ?? 0)))
                    {
                        // Check if the booking date is within the filter range
                        if ((input.StartBookingDate == DateTime.MinValue.Date || currentDate >= DateOnly.FromDateTime(input.StartBookingDate)) &&
                            (input.EndBookingDate == DateTime.MinValue.Date || currentDate <= DateOnly.FromDateTime(input.EndBookingDate)))
                        {
                            calendarBookings.Add(new BookingCalendarDto
                            {
                                BookingDate = currentDate,
                                StartTime = booking.StartTime,
                                EndTime = booking.EndTime,
                                CarModel = booking.Car?.Model ?? "Unknown"
                            });
                        }
                    }

                    // Move to the next booking date
                    currentDate = currentDate.AddDays(1);
                }
            }

            return calendarBookings;
        }

        public async Task<string> CreateBooking(CreateBookingDto newBooking)
        {
            // Checking Required fields here istead of use in DTO
            if (newBooking.BookingDate == DateTime.MinValue)
                return "BookingDate field is required.";
            if (newBooking.StartTime == TimeSpan.Zero)
                return "StartTime field is required.";
            if (newBooking.EndTime == TimeSpan.Zero)
                return "EndTime field is required.";
            if (newBooking.CarId == Guid.Empty)
                return "CarId field is required.";

            // Checking value range as valid
            if (newBooking.StartTime >= newBooking.EndTime)
                return "StartTime should be less than EndTime.";
            if (newBooking.BookingDate < DateTime.Now.Date)
                return "Booking date should be greater than or equal to today's date.";
            if (newBooking.DaysToRepeatOn.HasValue && (int)newBooking.DaysToRepeatOn > 127)
                return "DaysToRepeatOn should be 0 to 127. Please change.";

            // Checking Repeat Option if applicable like Daily or Weekly            
            if (newBooking.RepeatOption == RepeatOption.Daily || newBooking.RepeatOption == RepeatOption.Weekly)
            {
                if (newBooking.RepeatOption == RepeatOption.Weekly && (!newBooking.DaysToRepeatOn.HasValue || newBooking.DaysToRepeatOn == DaysOfWeek.None))
                    return "DaysToRepeatOn field is required for repeat option Weekly.";

                if (!newBooking.EndRepeatDate.HasValue)
                    return "EndRepeatDate field is required for repeat option.";
                else if (newBooking.EndRepeatDate.Value < newBooking.BookingDate)
                    return "EndRepeatDate should be greater than or equal to BookingDate.";
            }
            else
            {
                newBooking.EndRepeatDate = null;
                newBooking.DaysToRepeatOn = null;
            }

            // Listing as possible repeat booking date
            var bookingDateList = new List<DateOnly>();
            var bookingDate = newBooking.BookingDate;

            while (bookingDate <= (newBooking.EndRepeatDate ?? newBooking.BookingDate))
            {
                if (IsDayOfWeekMatch(DateOnly.FromDateTime(bookingDate), (int)(newBooking.DaysToRepeatOn ?? 0)))
                {
                    bookingDateList.Add(DateOnly.FromDateTime(bookingDate));
                }
                bookingDate = bookingDate.AddDays(1);
            }

            // Retriving all booking that match with booking time
            var bookingsQuery = await _context.Bookings
                .Where(b => b.CarId == newBooking.CarId &&
                    ((b.StartTime <= newBooking.StartTime && b.EndTime >= newBooking.EndTime) ||
                    (b.StartTime >= newBooking.StartTime && b.StartTime <= newBooking.EndTime) ||
                    (b.EndTime >= newBooking.StartTime && b.EndTime <= newBooking.EndTime)))
                .ToListAsync();

            // Checking conflict booking
            foreach (var booking in bookingsQuery)
            {
                var currentDate = booking.BookingDate;
                while (currentDate <= (booking.EndRepeatDate ?? booking.BookingDate))
                {
                    if (IsDayOfWeekMatch(currentDate, (int)(booking.DaysToRepeatOn ?? 0)))
                    {
                        if (bookingDateList.Contains(currentDate))
                            return $"Booking conflict with date {currentDate} & specified time range !";
                    }

                    currentDate = currentDate.AddDays(1);
                }
            }

            // Creating booking entity
            var createBooking = new Booking
            {
                Id = Guid.NewGuid(),
                CarId = newBooking.CarId,
                BookingDate = DateOnly.FromDateTime(newBooking.BookingDate),
                StartTime = newBooking.StartTime,
                EndTime = newBooking.EndTime,
                RepeatOption = newBooking.RepeatOption,
                EndRepeatDate = newBooking.EndRepeatDate.HasValue ? DateOnly.FromDateTime(newBooking.EndRepeatDate.Value) : (DateOnly?)null,
                DaysToRepeatOn = newBooking.DaysToRepeatOn,
                RequestedOn = DateTime.UtcNow,
            };

            // Saving booking to database
            await _context.Bookings.AddAsync(createBooking);
            await _context.SaveChangesAsync();

            return string.Empty;
        }

        // Helper function to check if the current day matches the DaysToRepeatOn bitmask
        private bool IsDayOfWeekMatch(DateOnly currentDate, int daysToRepeatOn)
        {
            if (daysToRepeatOn == 0)
                return true;

            int currentDayMask = 1 << (int)currentDate.DayOfWeek; // Get bitmask for current day

            return (daysToRepeatOn & (currentDayMask)) != 0; // Check if the current day is part of the recurrence
        }
    }
}