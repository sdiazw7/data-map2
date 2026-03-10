using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public abstract class BaseService(ILogger logger)
{
    protected readonly ILogger Logger = logger;
}
