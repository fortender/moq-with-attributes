# MoqWithAttributes
[![.NET](https://github.com/fortender/moq-with-attributes/actions/workflows/dotnet.yml/badge.svg)](https://github.com/fortender/moq-with-attributes/actions/workflows/dotnet.yml)

## Purpose
Create mocks via [Moq](https://github.com/devlooped/moq) and specify attributes that will be attached to the dynamically created proxy type.

## Motivation
[Moq](https://github.com/devlooped/moq) leverages [Castle.DynamicProxy](http://www.castleproject.org/projects/dynamicproxy/) for proxy creation. However, although DynamicProxy lets you specify attributes that will be attached to the proxy type, Moq *currently* does not allow you to do that out of the box.

If you need to work with attributes you may find yourself creating an additional wrapper around the proxy and attach the attributes on that type. That's indeed a solution I'd prefer having in my production code rather than this solution. See the **disclaimer** below.

## Usage
```csharp
using MoqWithAttributes.Moqtils;

var mock = new Mock<IProxyType>();
// Don't use mock.Object to initialize and get the object
// Use the following:
IProxyType instance = mock.GetObjectWithAttributes(() => new YourAttribute(arg1, arg2));
```

You have to ensure that mocks are not created in parallel. I will clarify on why that is a requirement soon.

## Disclaimer
**I will not be liable for any damages this code might cause!**

This project was created out of pure curiosity whether it is possible to manipulate the Moq proxy generation and leverages a few techniques in a way that makes it unsuitable for use in uncontrollable environments. A few of them are:
* Using non-public APIs
* Access of private / internal fields via reflection
* Generating IL code dynamically to change readonly private fields (this is basically used to implement an atomic swap of the attribute list on the `DynamicProxy.ProxyGenerationOptions` based on `Interlocked.Exchange`)
