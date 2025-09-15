
using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace ProfessorAPI.Binding
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FromFormJsonAttribute : Attribute, IBindingSourceMetadata
    {

        public BindingSource BindingSource => BindingSource.Form;
    }
}
