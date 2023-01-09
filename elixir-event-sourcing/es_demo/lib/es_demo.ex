defmodule EsDemo do
  def call(entity_type, id, command, es_type) do
    pid = EsDemo.BankAccounts.Supervisor.get_entity(entity_type, id, es_type)

    GenServer.call(pid, command)
  end

  def cast(entity_type, id, command, es_type) do
    pid = EsDemo.BankAccounts.Supervisor.get_entity(entity_type, id, es_type)

    GenServer.cast(pid, command)
  end
end
