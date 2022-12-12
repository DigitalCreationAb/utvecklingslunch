defmodule EsDemo do
  def call(entity_type, id, command) do
    pid = EsDemo.BankAccounts.Supervisor.get_entity(entity_type, id)

    GenServer.call(pid, {:execute, command})
  end
end
