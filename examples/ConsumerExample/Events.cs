namespace ConsumerExample;

using MediatR;
using Minid;

public record PaymentInitiated(Id Id, long Amount, string Currency, string Refererence) : INotification;