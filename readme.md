
[![Visual Studio Team services](https://img.shields.io/vso/build/sinnovations/c4ea4838-1bed-4dff-801b-4a20b7da1f0a/16.svg?style=flat-square&label=ServiceFabric.Gateway)]()
[![Myget Version](http://img.shields.io/myget/s-innovations/vpre/S-Innovations.ServiceFabric.Gateway.Common.svg?style=flat-square&label=myget:%20Gateway.Common)](https://www.myget.org/feed/s-innovations/package/nuget/S-Innovations.ServiceFabric.Gateway.Common)
[![Myget Version](http://img.shields.io/myget/s-innovations/vpre/S-Innovations.ServiceFabric.RegistrationMiddleware.AspNetCore.svg?style=flat-square&label=myget:%20RigistrationMiddleware.AspNetCore)](https://www.myget.org/feed/s-innovations/package/nuget/S-Innovations.ServiceFabric.RegistrationMiddleware.AspNetCore )
[![Myget Version](http://img.shields.io/myget/s-innovations/vpre/S-Innovations.ServiceFabric.RegistrationMiddleware.Owin.svg?style=flat-square&label=myget:%20RigistrationMiddleware.Owin)](https://www.myget.org/feed/s-innovations/package/nuget/S-Innovations.ServiceFabric.RegistrationMiddleware.Owin)

#S-Innovations ServiceFabric Gateway and Architecture Overview

My oppionated service fabric gateway solution that will provide delegation to microservices. This application is a gateway with nuget packages to with support microservices hosted in AspNetCore and Owin based pipeline. 

The gateway is monitoring other SF services that reply successfull on `/sf-gateway-metadata` over http. The microservice endpoints can provide metadata about which url prefixes it wants forwarded from the gateway.

## Goal
The goal for this project is

- [] to make a Service Fabric Application that can be the public endpoint of microservices and provide as little friction as possible when new services are added.
- [] make it easy adorptable for new startups/platforms with key incredients: IdentityService, Storage API and its own little provider manager.
- [] get som adorption and provide a cashflow back.

## TODO
- [x] Prefix Path matching
- [] Resource Provider Path Matching
- [] Rules for precendence when multiply service forwards are possible.
- [] Migrate S-Innovations.Identity to Idsvr4 and expose it on /idsrv prefix
- [] Migrate S-Innovations.MultitenantStorage to this architecture



### Prefix Path Mathcing
Currently services and instruct the gateway to forward all requests that starts with a given prefix. The most simple case.

### Resource Provider Path Matching
Being an Azure developer and working against the Azure Resource Manager in much of my work, I have grown very fond of the REST api behind it and for a few projects I been using a similar pattern in webapi. With this project I want to make this as easy as possible by adopting the Subscription,ResourceGroup and Providers entities. I will be using the term workset instead of resoure group, but I intent to make that customizable.

The following urls prefixes should be possible:
```
/subscriptions/{guid}/worksets/{worksetname}/providers/{providerid}/
/providers/{providerid}
/providers/
/worksets/
/subscriptions/
```
where only one service may register a provider id, such all requests to either
```
/subscriptions/{guid}/worksets/{worksetname}/providers/{providerid}/
/providers/{proiderid}/
```
is going to be forwared to the service that has registered the provider id.

`/providers` can be handled in the gateway service
`/subscriptions` can be handled in the idsrv service
`/worksets` can be handled in a data service.

### Data Service
Part of the architecture is a data service that instead of resource groups will handle generic data. Its an old project that just needs small updates around the url handling to fit this pattern. Its written using owin currently.

All requests that do not go to a provider, ect `/subscription/{guid}/worksets/{worksetname}/files`  will be picked up by this service and the essense of it is that it splis a azure storage account between subscriptions. 
The service also allows the normal .Net for Azure Storage Clients to be used with small customizations. Last but properly the feature I feel strongest about in the data service is that it translates all the XML to json, making it super easy to use from SPAs.



