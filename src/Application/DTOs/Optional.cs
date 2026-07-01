namespace SchoolMaster.Application.DTOs;

/// <summary>
/// Wraps a value in PATCH request DTOs to distinguish "field was absent from JSON"
/// (HasValue = false → keep existing) from "field was explicitly sent as null"
/// (HasValue = true, Value = null → clear the field).
/// </summary>
/// will create an Optional<T> instance only if the propety exists in the Json
/// if it doesn't it remains as the previous value
public readonly struct Optional<T>
{
    private readonly T? _value;
    public bool HasValue { get; }
    public T? Value => _value;

    public Optional(T? value)
    {
        _value = value;
        HasValue = true;
    }

    public static implicit operator Optional<T>(T? value) => new(value);
}
