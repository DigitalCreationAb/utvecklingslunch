defmodule EsDemoEventBus do
  @moduledoc false

  @behaviour Reactive.Persistence.EventBus

  def load_events(stream_name) do
    GenServer.call(Reactive.Persistence.InMemoryEventStore, {:load, stream_name})
  end

  def append_events(stream_name, events) do
    GenServer.call(Reactive.Persistence.InMemoryEventStore, {:append, stream_name, events})
  end
end
