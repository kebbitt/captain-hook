using System;
using CaptainHook.Common.Proposal;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

// ReSharper disable once CheckNamespace
public class HttpContractResolverTest
{
    [Fact, IsUnit]
    public void Test_Resolver_Ignores_JsonIgnoreAndHttpIgnore()
    {
        var payload = new TestPayload();
        var jsonOptions = new JsonSerializerSettings { ContractResolver = new HttpContractResolver() };

        var resultPayload = JsonConvert.DeserializeObject<TestPayload>(JsonConvert.SerializeObject(payload, jsonOptions), jsonOptions);

        resultPayload.ValidProperty.Should().Be(payload.ValidProperty);
        resultPayload.JsonIgnoreProperty.Should().NotBe(payload.JsonIgnoreProperty);
        resultPayload.HttpIgnoreProperty.Should().NotBe(payload.HttpIgnoreProperty);
    }
}

public class TestPayload
{
    public TestPayload()
    {
        ValidProperty = Lorem.GetSentence();
        JsonIgnoreProperty = Guid.NewGuid().ToString();
        HttpIgnoreProperty = Guid.NewGuid().ToString();
    }

    public string ValidProperty { get; set; }

    [JsonIgnore]
    public string JsonIgnoreProperty { get; set; }

    [HttpIgnore]
    public string HttpIgnoreProperty { get; set; }
}
