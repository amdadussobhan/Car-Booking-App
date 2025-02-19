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
        [HttpGet("Booking")]
        public async Task<IEnumerable<BookingCalendarDto>> GetCalendarBookings([FromQuery] BookingFilterDto input)
        {
            // Retrive booking data from database
            var bookingsQuery = await _context.Bookings
                .Include(b => b.Car) // include car details
                .ToListAsync();

            // Apply CardId filters if provided
            if (input.CarId != Guid.Empty)
                bookingsQuery = bookingsQuery.Where(b => b.CarId == input.CarId).ToList();
            
            // Apply StartBookingDate filters if provided
            if (input.StartBookingDate != DateOnly.MinValue)
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate >= input.StartBookingDate).ToList();
            
            // Apply EndBookingDate filters if provided
            if (input.EndBookingDate != DateOnly.MinValue)            
                bookingsQuery = bookingsQuery.Where(b => b.BookingDate <= input.EndBookingDate).ToList();
            
            var calendarBookings = new List<BookingCalendarDto>();

            // Generate calendar bookings
            foreach (var booking in bookingsQuery)
            {
                var currentDate = booking.BookingDate;
                while (currentDate <= (booking.EndRepeatDate ?? booking.BookingDate))
                {   
                    // Check if the booking date is within the filter range
                    if ((input.StartBookingDate == DateOnly.MinValue || currentDate >= input.StartBookingDate) &&
                        (input.EndBookingDate == DateOnly.MinValue || currentDate <= input.EndBookingDate))
                    {
                        calendarBookings.Add(new BookingCalendarDto
                        {
                            BookingDate = currentDate,
                            StartTime = booking.StartTime,
                            EndTime = booking.EndTime,
                            CarModel = booking.Car.Model
                        });
                    }

                    // Move to the next booking date
                    currentDate = booking.RepeatOption switch
                    {
                        RepeatOption.Daily => currentDate.AddDays(1),
                        RepeatOption.Weekly => currentDate.AddDays(7),
                        _ => currentDate.AddDays(1)
                    };
                }
            }

            return calendarBookings;
        }

        // POST: api/Bookings
        [HttpPost("Booking")]
        public async Task<IActionResult> PostBooking(CreateUpdateBookingDto booking)
        {
            // Step 1: Input validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Step 2: Check for booking conflicts
            var conflictingBooking = await _context.Bookings
                .Where(b => b.CarId == booking.CarId &&
                            (b.BookingDate == booking.BookingDate ||
                            (b.RepeatOption == RepeatOption.Daily && booking.RepeatOption == RepeatOption.Daily) ||
                            (b.RepeatOption == RepeatOption.Weekly && booking.RepeatOption == RepeatOption.Weekly)))
                .Where(b => b.StartTime < booking.EndTime && b.EndTime > booking.StartTime)
                .AnyAsync();

            if (conflictingBooking)
            {
                return Conflict("A booking conflict exists for this car in the specified date/time range.");
            }

            // Step 3: Create booking entity
            var newBooking = new Booking
            {
                Id = Guid.NewGuid(),
                CarId = booking.CarId,
                BookingDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                RepeatOption = booking.RepeatOption,
                EndRepeatDate = booking.EndRepeatDate,
                RequestedOn = DateTime.UtcNow,
            };

            // Step 4: Save booking to database
            await _context.Bookings.AddAsync(newBooking);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCalendarBookings), new { id = newBooking.Id }, booking);
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

            if(!bookings.Any())
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
                foreach(var booking in item.Value)
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
