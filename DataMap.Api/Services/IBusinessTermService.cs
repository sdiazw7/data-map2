using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IBusinessTermService
{
    Task<List<BusinessTermDto>> GetAllAsync(Guid workspaceId);
    Task<BusinessTermDto> CreateAsync(Guid workspaceId, BusinessTermCreateRequest request);
    Task MapToColumnAsync(Guid workspaceId, TermMappingRequest request);
}
