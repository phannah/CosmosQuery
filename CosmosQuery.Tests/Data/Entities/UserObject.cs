﻿namespace CosmosQuery.Tests.Data.Entities;

public sealed record UserObject
{
    public User User { get; init; } = default!;
}


