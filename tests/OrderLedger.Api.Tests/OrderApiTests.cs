using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderLedger.Api.Contracts;
using OrderLedger.Api.Tests.Infrastructure;

namespace OrderLedger.Api.Tests;

public sealed class OrderApiTests : IClassFixture<OrderLedgerApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public OrderApiTests(OrderLedgerApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrderPersistsOrderAndOutboxEvent()
    {
        var request = new CreateOrderRequest(
            "customer-123",
            [new CreateOrderItemRequest("SKU-API", 2, 19.95m)],
            "USD");

        var response = await _client.PostAsJsonAsync("/api/orders", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(39.90m, order.TotalAmount);

        var fetched = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(order.Id, fetched.Id);

        var outbox = await _client.GetFromJsonAsync<OutboxResponse>("/api/outbox", JsonOptions);
        Assert.NotNull(outbox);
        Assert.Contains(outbox.Items, item => item.OrderId == order.Id && item.Type == "OrderCreated");
    }

    [Fact]
    public async Task ListOrdersReturnsCreatedOrders()
    {
        var first = await CreateOrderAsync("customer-list-a", "SKU-LIST-A");
        var second = await CreateOrderAsync("customer-list-b", "SKU-LIST-B");

        var list = await _client.GetFromJsonAsync<OrderListResponse>("/api/orders", JsonOptions);

        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.Id == first.Id && item.CustomerId == "customer-list-a");
        Assert.Contains(list.Items, item => item.Id == second.Id && item.CustomerId == "customer-list-b");
    }

    [Fact]
    public async Task IdempotencyKeyReturnsExistingOrder()
    {
        var request = new CreateOrderRequest(
            "customer-456",
            [new CreateOrderItemRequest("SKU-IDEMPOTENT", 1, 42m)],
            "USD");

        using var firstMessage = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
        firstMessage.Headers.Add("Idempotency-Key", "demo-key");
        firstMessage.Content = JsonContent.Create(request, options: JsonOptions);

        using var secondMessage = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
        secondMessage.Headers.Add("Idempotency-Key", "demo-key");
        secondMessage.Content = JsonContent.Create(request, options: JsonOptions);

        var first = await _client.SendAsync(firstMessage);
        var second = await _client.SendAsync(secondMessage);

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstOrder = await first.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        var secondOrder = await second.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        Assert.NotNull(firstOrder);
        Assert.NotNull(secondOrder);
        Assert.Equal(firstOrder.Id, secondOrder.Id);

        var outbox = await _client.GetFromJsonAsync<OutboxResponse>("/api/outbox", JsonOptions);
        Assert.NotNull(outbox);
        Assert.Single(outbox.Items, item => item.OrderId == firstOrder.Id);
    }

    private async Task<OrderResponse> CreateOrderAsync(string customerId, string sku)
    {
        var request = new CreateOrderRequest(
            customerId,
            [new CreateOrderItemRequest(sku, 1, 25m)],
            "USD");

        var response = await _client.PostAsJsonAsync("/api/orders", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        Assert.NotNull(order);
        return order;
    }
}
