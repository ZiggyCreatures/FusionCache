using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace WebAppTest.Controllers;

[ApiController]
[Route("[controller]")]
public class MvcController : ControllerBase
{
	private readonly ILogger<MvcController> _logger;

	public MvcController(ILogger<MvcController> logger)
	{
		_logger = logger;
	}

	[HttpGet("now")]
	public DateTimeOffset Now()
	{
		return DateTimeOffset.UtcNow;
	}

	[HttpGet("now-cached-2")]
	[OutputCache(PolicyName = "Expire2")]
	public DateTimeOffset NowCached2()
	{
		return DateTimeOffset.UtcNow;
	}

	[HttpGet("now-cached-5")]
	[OutputCache(PolicyName = "Expire5")]
	public DateTimeOffset NowCached5()
	{
		return DateTimeOffset.UtcNow;
	}
}
