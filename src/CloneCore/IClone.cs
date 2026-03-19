namespace Clone;

public interface IClone<out T> where T : class, new()
{
    T Clone();
}
