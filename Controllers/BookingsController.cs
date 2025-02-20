using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using Wafi.SampleTest.Dtos;
using Wafi.SampleTest.Entities;

namespace Wafi.SampleTest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly WafiDbContext _context;

        public BookingsController(WafiDbContext context)
        {
            _context = context;
        }

        // GET: api/Bookings
        [HttpGet("List")]
        public async Task<IEnumerable<BookingCalendarDto>> GetCalendarBookings([FromQuery] BookingFilterDto input)
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

        // POST: api/Bookings
        [HttpPost("Create")]
        public async Task<IActionResult> PostBooking(CreateBookingDto newBooking)
        {
            // Validating Input
            if (!ModelState.IsValid)
                return BadRequest("Something went wrong ! Please try again.");

            // Checking Required fields here istead of use in DTO
            if (newBooking.BookingDate == DateTime.MinValue)
                return BadRequest("BookingDate field is required.");
            if (newBooking.StartTime == TimeSpan.Zero)
                return BadRequest("StartTime field is required.");
            if (newBooking.EndTime == TimeSpan.Zero)
                return BadRequest("EndTime field is required.");
            if (newBooking.CarId == Guid.Empty)
                return BadRequest("CarId field is required.");

            // Checking value range as valid
            if (newBooking.StartTime >= newBooking.EndTime)
                return BadRequest("StartTime should be less than EndTime.");
            if (newBooking.BookingDate < DateTime.Now.Date)
                return BadRequest("Booking date should be greater than or equal to today's date.");
            if (newBooking.DaysToRepeatOn.HasValue && (int)newBooking.DaysToRepeatOn > 127)
                return BadRequest("DaysToRepeatOn should be 0 to 127. Please change.");

            // Checking Repeat Option if applicable like Daily or Weekly            
            if (newBooking.RepeatOption == RepeatOption.Daily || newBooking.RepeatOption == RepeatOption.Weekly)
            {
                if (newBooking.RepeatOption == RepeatOption.Weekly && (!newBooking.DaysToRepeatOn.HasValue || newBooking.DaysToRepeatOn == DaysOfWeek.None))
                    return BadRequest("DaysToRepeatOn field is required for repeat option Weekly.");

                if (!newBooking.EndRepeatDate.HasValue)
                    return BadRequest("EndRepeatDate field is required for repeat option.");
                else if (newBooking.EndRepeatDate.Value < newBooking.BookingDate)
                    return BadRequest("EndRepeatDate should be greater than or equal to BookingDate.");
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
                            return Conflict($"Booking conflict with date {currentDate} & specified time range !");
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

            return CreatedAtAction(nameof(GetCalendarBookings), new { CarId = createBooking.CarId }, newBooking);
        }


        // Helper function to check if the current day matches the DaysToRepeatOn bitmask
        private bool IsDayOfWeekMatch(DateOnly currentDate, int daysToRepeatOn)
        {
            if (daysToRepeatOn == 0)
                return true;

            int currentDayMask = 1 << (int)currentDate.DayOfWeek; // Get bitmask for current day

            return (daysToRepeatOn & (currentDayMask)) != 0; // Check if the current day is part of the recurrence
        }

        // GET: api/SeedData
        // For test purpose
        [HttpGet("SeedData")]
        public async Task<IEnumerable<BookingCalendarDto>> GetSeedData()
        {
            var cars = await _context.Cars.ToListAsync();

            if (!cars.Any())
            {
                cars = GetCars().ToList();
                await _context.Cars.AddRangeAsync(cars);
                await _context.SaveChangesAsync();
            }

            var bookings = await _context.Bookings.ToListAsync();

            if (!bookings.Any())
            {
                bookings = GetBookings().ToList();

                await _context.Bookings.AddRangeAsync(bookings);
                await _context.SaveChangesAsync();
            }

            var calendar = new Dictionary<DateOnly, List<Booking>>();

            foreach (var booking in bookings)
            {
                var currentDate = booking.BookingDate;
                while (currentDate <= (booking.EndRepeatDate ?? booking.BookingDate))
                {
                    if (!calendar.ContainsKey(currentDate))
                        calendar[currentDate] = new List<Booking>();

                    calendar[currentDate].Add(booking);

                    currentDate = booking.RepeatOption switch
                    {
                        RepeatOption.Daily => currentDate.AddDays(1),
                        RepeatOption.Weekly => currentDate.AddDays(7),
                        _ => booking.EndRepeatDate.HasValue ? booking.EndRepeatDate.Value.AddDays(1) : currentDate.AddDays(1)
                    };
                }
            }

            List<BookingCalendarDto> result = new List<BookingCalendarDto>();

            foreach (var item in calendar)
            {
                foreach (var booking in item.Value)
                {
                    result.Add(new BookingCalendarDto { BookingDate = booking.BookingDate, CarModel = booking.Car.Model, StartTime = booking.StartTime, EndTime = booking.EndTime });
                }
            }

            return result;
        }

        #region Sample Data

        private IList<Car> GetCars()
        {
            var cars = new List<Car>
            {
                new Car { Id = Guid.NewGuid(), Make = "Toyota", Model = "Corolla" },
                new Car { Id = Guid.NewGuid(), Make = "Honda", Model = "Civic" },
                new Car { Id = Guid.NewGuid(), Make = "Ford", Model = "Focus" }
            };

            return cars;
        }

        private IList<Booking> GetBookings()
        {
            var cars = GetCars();

            var bookings = new List<Booking>
            {
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 5), StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), RepeatOption = RepeatOption.DoesNotRepeat, RequestedOn = DateTime.Now, CarId = cars[0].Id, Car = cars[0] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 10), StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(16, 0, 0), RepeatOption = RepeatOption.Daily, EndRepeatDate = new DateOnly(2025, 2, 20), RequestedOn = DateTime.Now, CarId = cars[1].Id, Car = cars[1] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 2, 15), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 30, 0), RepeatOption = RepeatOption.Weekly, EndRepeatDate = new DateOnly(2025, 3, 31), RequestedOn = DateTime.Now, DaysToRepeatOn = DaysOfWeek.Monday, CarId = cars[2].Id,  Car = cars[2] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 1), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(13, 0, 0), RepeatOption = RepeatOption.DoesNotRepeat, RequestedOn = DateTime.Now, CarId = cars[0].Id, Car = cars[0] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 7), StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(10, 0, 0), RepeatOption = RepeatOption.Weekly, EndRepeatDate = new DateOnly(2025, 3, 28), RequestedOn = DateTime.Now, DaysToRepeatOn = DaysOfWeek.Friday, CarId = cars[1].Id, Car = cars[1] },
                new Booking { Id = Guid.NewGuid(), BookingDate = new DateOnly(2025, 3, 15), StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(17, 0, 0), RepeatOption = RepeatOption.Daily, EndRepeatDate = new DateOnly(2025, 3, 20), RequestedOn = DateTime.Now, CarId = cars[2].Id,  Car = cars[2] }
            };

            return bookings;
        }

        #endregion
    }
}
