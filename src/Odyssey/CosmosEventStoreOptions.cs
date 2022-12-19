namespace Odyssey;

using Microsoft.Azure.Cosmos;

public class CosmosEventStoreOptions
{
    public CosmosEventStoreOptions()
    {
        DatabaseId = "odyssey";
        AutoCreateDatabase = true;
        ContainerId = "events";
        AutoCreateContainer = true;
    }

    public string DatabaseId { get; set; }
    public bool AutoCreateDatabase { get; set; }
    public string ContainerId { get; set; }
    public bool AutoCreateContainer { get; set; }
    public ThroughputProperties? DatabaseThroughputProperties { get; set; }
}