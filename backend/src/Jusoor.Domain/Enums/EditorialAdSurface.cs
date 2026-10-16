using System.Text.Json.Serialization;

namespace Jusoor.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<EditorialAdSurface>))]
public enum EditorialAdSurface
{
    Web = 1,
    Mobile = 2
}
