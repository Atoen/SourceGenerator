namespace TextLocalizer.Types;

public interface ILocalizedTextProvider
{
    string? this[int key] { get; }
}
