using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace ProfessorAPI.Binding
{
    public class FromFormJsonModelBinderProvider : IModelBinderProvider
    {

        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var hasAttribute = context.Metadata
                .ContainerType?
                .GetProperty(context.Metadata.PropertyName!)
                ?.GetCustomAttributes(typeof(FromFormJsonAttribute), false)
                .Any() ?? false;

            return hasAttribute ? new FromFormJsonModelBinder() : null;
        }

    }
}
