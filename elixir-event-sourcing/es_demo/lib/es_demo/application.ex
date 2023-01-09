defmodule EsDemo.Application do
  @moduledoc false

  use Application

  def start(_type, _args) do
    children = [
      EsDemo.BankAccounts.Supervisor,
      {Eventize.Persistence.InMemoryEventStore, name: InMemoryEventStore},
      {Spear.Connection,
       name: EventStoreDbConnection, connection_string: "esdb://admin:changeit@127.0.0.1:2113"},
      {Eventize.Eventstore.EventStoreDB, name: EventStoreDB, event_store: EventStoreDbConnection}
    ]

    opts = [strategy: :one_for_one, name: EsDemo.Supervisor]
    Supervisor.start_link(children, opts)
  end
end
