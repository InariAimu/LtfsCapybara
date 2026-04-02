namespace LtfsServer.Features.AI.Tools;


[AttributeUsage(AttributeTargets.Class)]
public class AIToolModuleAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}


[AttributeUsage(AttributeTargets.Method)]
public class AIToolAttribute : Attribute
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}


[AttributeUsage(AttributeTargets.Parameter)]
public class AIToolParamAttribute : Attribute
{
    public required string Description { get; init; }
}
