namespace TextLocalizer.Types;

public readonly record struct StringResourceId
{
    public readonly int Id;

    public StringResourceId(int id)
    {
        Id = id;
    }

    public static implicit operator int(StringResourceId stringResourceId)
    {
        return stringResourceId.Id;
    }

    public static explicit operator StringResourceId(int id)
    {
        return new StringResourceId(id);
    }
}
