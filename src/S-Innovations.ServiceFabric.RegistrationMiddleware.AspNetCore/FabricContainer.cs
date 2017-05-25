using Microsoft.Practices.Unity;
using SInnovations.ServiceFabric.Unity;
using SInnovations.Unity.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore
{
    public class FabricContainer : UnityContainer, IServiceScopeInitializer
    {

        public FabricContainer()
        {
            this.RegisterInstance<IServiceScopeInitializer>(this);
            this.AsFabricContainer();
        }
        public IUnityContainer InitializeScope(IUnityContainer container)
        {
            return container.WithExtension();
        }
    }
}
