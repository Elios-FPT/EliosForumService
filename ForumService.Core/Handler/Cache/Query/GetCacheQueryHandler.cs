using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Cache.Query;

namespace ForumService.Core.Handler.Cache.Query
{
    public class GetCacheQueryHandler : IQueryHandler<GetCacheQuery, BaseResponseDto<string>>
    {
        private readonly IAppCacheService _cacheService;

        public GetCacheQueryHandler(IAppCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public async Task<BaseResponseDto<string>> Handle(GetCacheQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Key.ToString()))
            {
                return new BaseResponseDto<string>
                {
                    Status = 400,
                    Message = "Cache key cannot be null or empty.",
                    ResponseData = null
                };
            }

            try
            {
                var result = await _cacheService.GetAsync<string>(request.Key.ToString());
                return new BaseResponseDto<string>
                {
                    Status = 200,
                    Message = result != null ? "Cache value retrieved successfully." : "Cache key not found.",
                    ResponseData = result
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<string>
                {
                    Status = 500,
                    Message = $"Failed to retrieve cache value: {ex.Message}",
                    ResponseData = null
                };
            }
        }
    }
}
