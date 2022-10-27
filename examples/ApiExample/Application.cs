namespace ApiExample.Onboarding;

using Minid;
using Odyssey.Model;

public class Application : Aggregate<Id>
{
    public ApplicationState State { get; private set; }

    public static Application Initiate(Id platformId, string email, string firstName, string lastName)
    {
        var initiated = new ApplicationInitiated(
            Id.NewId("app"),
            platformId,
            email,
            firstName,
            lastName,
            DateTime.UtcNow
        );

        var application = new Application();
        application.Raise(initiated);

        return application;
    }

    public void Start(string ipAddress)
    {
        Raise(new ApplicationStarted(Id, ipAddress, DateTime.UtcNow));
    }

    public void InviteUbo(string firstName, string lastName, string email)
    {
        Raise(new UboAdded(firstName, lastName, email));
    }

    protected override void When(object @event)
    {
        switch (@event)
        {
            case ApplicationInitiated initiated:
                OnInitiated(initiated);
                break;
            case ApplicationStarted started:
                OnStarted(started);
                break;
        }
    }

    private void OnInitiated(ApplicationInitiated initiated)
    {
        Id = initiated.ApplicationId;
        State = ApplicationState.Initiated;
    }

    private void OnStarted(ApplicationStarted _)
    {
        State = ApplicationState.Started;
    }
}

public enum ApplicationState
{
    Initiated,
    Started
}

public record ApplicationInitiated(Id ApplicationId, Id PlatformId, string Email, string FirstName, string LastName, DateTime InitiatedOn);
public record ApplicationStarted(Id ApplicationId, string IPAddress, DateTime StartedOn);
public record UboAdded(string FirstName, string LastName, string Email);
