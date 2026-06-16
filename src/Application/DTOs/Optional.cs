namespace SchoolMaster.Application.DTOs;

/// <summary>
/// Wraps a value in PATCH request DTOs to distinguish "field was absent from JSON"
/// (HasValue = false → keep existing) from "field was explicitly sent as null"
/// (HasValue = true, Value = null → clear the field).
/// </summary>
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
