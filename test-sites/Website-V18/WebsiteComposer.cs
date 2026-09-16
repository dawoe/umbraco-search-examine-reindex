using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Search.BackOffice.DependencyInjection;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Provider.Examine.DependencyInjection;

namespace Website_V18
{
	public class WebsiteComposer : IComposer
	{
		public void Compose(IUmbracoBuilder builder)
		{
			builder
				// add core services for search abstractions
				.AddSearchCore()
				.AddBackOfficeSearch()
				// add the Examine search provider
				.AddExamineSearchProvider();
		}
	}
}
