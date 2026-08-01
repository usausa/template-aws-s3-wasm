using Amazon.Lambda.Serialization.SystemTextJson;

using Backend;

[assembly: System.CLSCompliant(false)]

// Source-generated serialization keeps the handler free of reflection-based JSON.
[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<FunctionSerializerContext>))]
