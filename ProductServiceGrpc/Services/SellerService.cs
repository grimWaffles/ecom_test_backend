using ProductServiceGrpc.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ProductServiceGrpc.Repository;
using SellerServiceGrpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProductServiceGrpc.Services
{
    public class SellerService : Seller.SellerBase
    {
        private readonly ISellerRepository _repo;

        public SellerService(ISellerRepository repo)
        {
            _repo = repo;
        }

        public override async Task<SellerResponse> CreateSeller(SellerRequest request, ServerCallContext context)
        {
            try
            {
                var seller = new SellerModel
                {
                    CompanyName = request.Dto.CompanyName,
                    Address = request.Dto.Address,
                    MobileNo = request.Dto.MobileNo,
                    Email = request.Dto.Email,
                    Rating = (decimal)request.Dto.Rating,
                    CreatedBy = request.UserId,
                    CreatedDate = DateTime.UtcNow
                };

                var result = await _repo.CreateSellerAsync(seller);

                return new SellerResponse
                {
                    Status = 1,
                    ErrorMessage = "Seller created successfully",
                    Dto = MapToDto(result)
                };
            }
            catch (Exception ex)
            {
                return new SellerResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to create seller: {ex.Message}"
                };
            }
        }

        public override async Task<SellerDto> GetSellerById(SellerSingleRequest request, ServerCallContext context)
        {
            try
            {
                var seller = await _repo.GetSellerByIdAsync(request.Id);
                if (seller == null)
                    throw new RpcException(new Status(StatusCode.NotFound, "Seller not found"));

                return MapToDto(seller);
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error fetching seller: {ex.Message}"));
            }
        }

        public override async Task<SellerMultipleResponse> GetAllSellers(Empty request, ServerCallContext context)
        {
            try
            {
                var sellers = await _repo.GetAllSellersAsync();
                var response = new SellerMultipleResponse();
                response.Sellers.AddRange(sellers.Select(MapToDto));
                return response;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving sellers: {ex.Message}"));
            }
        }

        public override async Task<SellerResponse> UpdateSeller(SellerRequest request, ServerCallContext context)
        {
            try
            {
                var updatedSeller = new SellerModel
                {
                    Id = request.Dto.Id,
                    CompanyName = request.Dto.CompanyName,
                    Address = request.Dto.Address,
                    MobileNo = request.Dto.MobileNo,
                    Email = request.Dto.Email,
                    Rating = (decimal)request.Dto.Rating,
                    ModifiedBy = request.UserId,
                    ModifiedDate = DateTime.UtcNow
                };

                var success = await _repo.UpdateSellerAsync(updatedSeller);

                if (!success)
                {
                    return new SellerResponse
                    {
                        Status = -1,
                        ErrorMessage = "Seller not found or update failed"
                    };
                }

                return new SellerResponse
                {
                    Status = 1,
                    ErrorMessage = "Seller updated successfully",
                    Dto = request.Dto
                };
            }
            catch (Exception ex)
            {
                return new SellerResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to update seller: {ex.Message}"
                };
            }
        }

        public override async Task<SellerResponse> DeleteSeller(SellerDeleteRequest request, ServerCallContext context)
        {
            try
            {
                var success = await _repo.DeleteSellerAsync(request.Id, request.UserId);
                if (!success)
                {
                    return new SellerResponse
                    {
                        Status = -1,
                        ErrorMessage = "Seller not found or already deleted"
                    };
                }

                return new SellerResponse
                {
                    Status = 1,
                    ErrorMessage = "Seller deleted successfully",
                    Dto = new SellerDto { Id = request.Id }
                };
            }
            catch (Exception ex)
            {
                return new SellerResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to delete seller: {ex.Message}"
                };
            }
        }

        private SellerDto MapToDto(SellerModel seller)
        {
            return new SellerDto
            {
                Id = seller.Id,
                CompanyName = seller.CompanyName,
                Address = seller.Address,
                MobileNo = seller.MobileNo,
                Email = seller.Email,
                Rating = (double)seller.Rating
            };
        }
    }
}

