using Microsoft.OpenApi.Models;

namespace Tekcari.Gapi.Generators
{
	public interface ITypeNameAdapter
	{
		string Map(OpenApiSchema typeInfo, IGeneratorSettings settings);
	}

	public interface ITypeNameAdapter<TSettings> where TSettings : IGeneratorSettings
	{
		string Map(OpenApiSchema typeInfo, TSettings settings);
	}
}