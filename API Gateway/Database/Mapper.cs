// using EfCoreTutorial.Dtos;
// using ProductServiceGrpc.Models;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;
// using static EfCoreTutorial.Entity.Enums;

// namespace EfCoreTutorial.Helpers
// {
//     public class Mapper
//     {
//         public OrderItemDto MapToOrderItemDto(OrderItem item)
//         {
//             return new OrderItemDto()
//             {
//                 OrderId = item.OrderId,
//                 Quantity = item.Quantity,
//                 GrossAmount = item.GrossAmount,
//                 ProductName = item.Product.Name,
//                 Description = item.Product.Description ?? "",
//                 CategoryName = item.Product.ProductCategory.CategoryName
//             };
//         }

//         public OrderInformationDto MapToOrderInformationDto(List<OrderItemDto> dtoItems, Order o)
//         {
//             return new OrderInformationDto()
//             {
//                 OrderId = o.Id,
//                 OrderDate = o.OrderDate,
//                 OrderCounter = o.OrderCounter,
//                 OrderStatus = o.Status,
//                 NetAmount = o.NetAmount,
//                 CustomerName = o.User.Username,
//                 Email = o.User.Email,
//                 MobileNo = o.User.MobileNo,
//                 OrderItems = dtoItems ?? new List<OrderItemDto>()
//             };
//         }
//     }
// }
