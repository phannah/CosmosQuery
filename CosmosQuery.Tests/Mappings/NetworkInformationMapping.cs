﻿using CosmosQuery.Tests.Data.Entities;
using CosmosQuery.Tests.Data.Models;

namespace CosmosQuery.Tests.Mappings;

internal sealed class NetworkInformationMapping : Profile
{
    public NetworkInformationMapping()
    {
        CreateMap<NetworkInformation, NetworkInformationModel>()
            .ForAllMembers(opts => opts.ExplicitExpansion());
    }
}
