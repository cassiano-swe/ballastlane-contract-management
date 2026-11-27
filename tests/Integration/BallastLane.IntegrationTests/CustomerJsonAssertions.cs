using System.Text.Json;
using Xunit;

namespace BallastLane.IntegrationTests;

internal static class CustomerJsonAssertions
{
    public static void MatchesWriteResponse(JsonElement write, JsonElement read, bool active)
    {
        Assert.Equal(7, write.EnumerateObject().Count());
        Assert.False(write.TryGetProperty("active", out _));
        Assert.Equal(8, read.EnumerateObject().Count());
        Assert.Equal(active, read.GetProperty("active").GetBoolean());
        foreach (var property in write.EnumerateObject())
            Assert.True(JsonElement.DeepEquals(property.Value, read.GetProperty(property.Name)), property.Name);
    }
}
