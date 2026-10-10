namespace JRK.JevRunner.Annotation;


/// <summary>
/// Decorate a class that serves as a query
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class JevQueryAttribute : Attribute
{
    
}


public interface IQuestionAttribute
{
    
}

[AttributeUsage(AttributeTargets.Class)]
public class NoulQuestionAttribute : Attribute, IQuestionAttribute
{
    
}
