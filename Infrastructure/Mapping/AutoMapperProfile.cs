using AutoMapper;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;

namespace HotelManagement.Infrastructure.Mapping
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // Hotel mappings
            CreateMap<Hotel, HotelDto>()
                .ForMember(dest => dest.OwnerName, opt => opt.MapFrom(src => src.Owner != null ? src.Owner.FullName : null))
                .ForMember(dest => dest.TotalRooms, opt => opt.MapFrom(src => src.Rooms.Count))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Reservations.Count));
            
            CreateMap<HotelDto, Hotel>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Don't map Id (it's the primary key)
                .ForMember(dest => dest.Owner, opt => opt.Ignore()) // Don't map navigation properties
                .ForMember(dest => dest.Rooms, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()); // Set in service
            
            // Room mappings
            CreateMap<Room, RoomDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Hotel != null ? src.Hotel.Name : null))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Reservations.Count));
            
            CreateMap<RoomDto, Room>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Don't map Id (it's the primary key)
                .ForMember(dest => dest.Hotel, opt => opt.Ignore()) // Don't map navigation properties
                .ForMember(dest => dest.Reservations, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.LastCleaned, opt => opt.Ignore()) // Managed separately
                .ForMember(dest => dest.LastMaintenance, opt => opt.Ignore()); // Managed separately
            
            // Guest mappings
            CreateMap<Guest, GuestDto>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Hotel != null ? src.Hotel.Name : null))
                .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedBy != null ? src.CreatedBy.FullName : null))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Reservations.Count));
            
            CreateMap<GuestDto, Guest>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Don't map Id (it's the primary key)
                .ForMember(dest => dest.User, opt => opt.Ignore()) // Don't map navigation properties
                .ForMember(dest => dest.Hotel, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.LastStayDate, opt => opt.Ignore()) // Managed separately
                .ForMember(dest => dest.CreatedByUserId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.HotelId, opt => opt.Ignore()); // Controlled in service
            
            // Reservation mappings (manual mapping used in service, but configured for consistency)
            CreateMap<Reservation, ReservationDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Hotel != null ? src.Hotel.Name : null))
                .ForMember(dest => dest.RoomNumber, opt => opt.MapFrom(src => src.Room != null ? src.Room.RoomNumber : null))
                .ForMember(dest => dest.GuestName, opt => opt.MapFrom(src => src.Guest != null ? src.Guest.FirstName + " " + src.Guest.LastName : null))
                .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedBy != null ? src.CreatedBy.FullName : null));
            
            CreateMap<CreateReservationDto, Reservation>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Hotel, opt => opt.Ignore())
                .ForMember(dest => dest.Room, opt => opt.Ignore())
                .ForMember(dest => dest.Guest, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedByUserId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.Status, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.RemainingAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PaymentStatus, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ConfirmedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CheckedInAt, opt => opt.Ignore())
                .ForMember(dest => dest.CheckedOutAt, opt => opt.Ignore())
                .ForMember(dest => dest.CancelledAt, opt => opt.Ignore())
                .ForMember(dest => dest.CancellationReason, opt => opt.Ignore())
                .ForMember(dest => dest.DiscountAmount, opt => opt.Ignore())
                .ForMember(dest => dest.DiscountReason, opt => opt.Ignore())
                .ForMember(dest => dest.ExtraCharges, opt => opt.Ignore())
                .ForMember(dest => dest.ExtraChargesNotes, opt => opt.Ignore());
        }
    }
}