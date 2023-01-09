defmodule EsDemo.BankAccounts.Supervisor do
  @moduledoc false

  use DynamicSupervisor

  def start_link(_) do
    DynamicSupervisor.start_link(__MODULE__, :ok, name: __MODULE__)
  end

  def child_spec() do
    %{
      id: __MODULE__,
      start: {__MODULE__, :start_link, []},
      type: :supervisor
    }
  end

  def get_entity(type, id, :es_db) do
    case DynamicSupervisor.start_child(__MODULE__, {type, %{id: id, event_bus: EventStoreDB}}) do
      {:ok, pid} -> pid
      {:error, {:already_started, pid}} -> pid
    end
  end

  def get_entity(type, id, _) do
    case DynamicSupervisor.start_child(
           __MODULE__,
           {type, %{id: id, event_bus: InMemoryEventStore}}
         ) do
      {:ok, pid} -> pid
      {:error, {:already_started, pid}} -> pid
    end
  end

  @impl true
  def init(_) do
    DynamicSupervisor.init(strategy: :one_for_one)
  end
end
