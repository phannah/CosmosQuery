﻿namespace CosmosQuery
{
    internal class OrderBySetting
    {
        public string Name { get; set; }
        public OrderBySetting ThenBy { get; set; }
    }
}
