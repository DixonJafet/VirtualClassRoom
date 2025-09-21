using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace ProfessorAPI.Binding
{
    public class FromFormJsonModelBinder : IModelBinder
    {

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }

            var rawValue = valueProviderResult.FirstValue;
            if (string.IsNullOrEmpty(rawValue))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            try
            {
                var result = JsonSerializer.Deserialize(rawValue, bindingContext.ModelType);
                bindingContext.Result = ModelBindingResult.Success(result);
            }
            catch (Exception ex)
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, ex.Message);
                bindingContext.Result = ModelBindingResult.Failed();
            }

            return Task.CompletedTask;
        }




    }
}
