// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attributebefore", "before");
            builder.AddAttribute(2, "attributeafter", "after");
            builder.SetKey(
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                    someObject

#line default
#line hidden
#nullable disable
            );
            builder.AddContent(3, "Hello");
            builder.CloseElement();
        }
        #pragma warning restore 1998
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
            
    private object someObject = new object();

#line default
#line hidden
#nullable disable
    }
}
#pragma warning restore 1591