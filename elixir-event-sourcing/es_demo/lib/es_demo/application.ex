defmodule EsDemo.Application do
  @moduledoc false

  use Application

  def start(_type, _args) do
    children = [
      EsDemo.BankAccounts.Supervisor,
      {Reactive.Persistence.InMemoryEventStore, name: Reactive.Persistence.InMemoryEventStore}
    ]

    opts = [strategy: :one_for_one, name: EsDemo.Supervisor]
    Supervisor.start_link(children, opts)
  end
end
