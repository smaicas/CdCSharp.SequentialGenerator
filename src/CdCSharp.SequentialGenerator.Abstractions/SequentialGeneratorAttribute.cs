namespace CdCSharp.SequentialGenerator.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class SequentialGeneratorAttribute : Attribute
{
    public int Order { get; }

    public SequentialGeneratorAttribute(int order) => Order = order;
}
