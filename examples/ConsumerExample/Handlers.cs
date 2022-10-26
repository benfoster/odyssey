namespace ConsumerExample;

using System.Threading;
using System.Threading.Tasks;
using MediatR;

public class PaymentInitiatedHandler : INotificationHandler<PaymentInitiated>
{
    private readonly ILogger<PaymentInitiatedHandler> _logger;

    public PaymentInitiatedHandler(ILogger<PaymentInitiatedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PaymentInitiated @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Payment {PaymentId} was initiated with amount {Amount}{Currency}", @event.Id, @event.Amount, @event.Currency);
        return Task.CompletedTask;
    }
}
