defmodule EsDemo.BankAccounts.AccountWithBehavior do
  @moduledoc false

  use Reactive.Entities.PersistedEntity

  def child_spec(%{:id => id} = data) do
    %{
      id: id,
      start: {__MODULE__, :start_link, [data]},
      type: :worker
    }
  end

  def start_link(%{:id => id} = data) do
    GenServer.start_link(
      __MODULE__,
      data,
      name: {:global, id}
    )
  end

  def start(_) do
    {EsDemo.BankAccounts.AccountWithBehavior.NotOpened, %{balance: 0}}
  end

  defmodule NotOpened do
    def execute_call({:open, %{:customer => customer}}, %{
          :id => id
        }) do
      {[{:account_opened, %{id: id, customer: customer}}],
       %{id: id, customer: customer, time_stamp: DateTime.utc_now()}}
    end

    def execute_call(_command, _state) do
      {:error, "This account doesn't exist"}
    end
  end

  defmodule Opened do
    def execute_call({:deposit, %{:amount => amount}}, %{
          :id => id,
          :state => %{:balance => current_balance}
        }) do
      {[{:money_deposited_to_account, %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}],
       %{id: id, balance: current_balance + amount}}
    end

    def execute_call({:withdraw, %{:amount => amount}}, %{
          :id => id,
          :state => %{:balance => current_balance}
        }) do
      case current_balance do
        balance when balance >= amount ->
          {[
             {:money_withdrawn_from_account,
              %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
           ], %{id: id, balance: current_balance - amount}}

        _ ->
          {:error, "Not enough money in account"}
      end
    end

    def execute_call(:close, %{
          :id => id,
          :state => %{:balance => current_balance}
        }) do
      case current_balance do
        0 ->
          {[{:account_closed, %{id: id}}], %{id: id, time_stamp: DateTime.utc_now()}}

        balance when balance > 0 ->
          {:error, "Can't close account with money left"}

        balance when balance < 0 ->
          {:error, "Can't close account with outstanding dept"}
      end
    end

    def execute_call(:suspend, %{
          :id => id
        }) do
      {[{:account_suspended, %{id: id}}], %{id: id, time_stamp: DateTime.utc_now()}}
    end

    def execute_call(:get_balance, %{:id => id, :state => %{:balance => current_balance}}) do
      %{id: id, balance: current_balance}
    end
  end

  defmodule Closed do
    def execute_call({:deposit, %{:amount => amount}}, %{
          :id => id
        }) do
      {:error, "Can't deposit money to a closed account"}
    end

    def execute_call({:withdraw, %{:amount => amount}}, %{
          :id => id
        }) do
      {:error, "Can't withdraw money from a closed account"}
    end

    def execute_call(:close, %{
          :id => id
        }) do
      {:error, "This account is already closed"}
    end

    def execute_call(:suspend, %{:id => id}) do
      {:error, "Can't suspend a closed account"}
    end

    def execute_call(:get_balance, %{:id => id}) do
      %{id: id, balance: 0}
    end
  end

  defmodule Suspended do
    def execute_call({:deposit, %{:amount => amount}}, %{
          :id => id
        }) do
      {:error, "Can't deposit money into a suspended account"}
    end

    def execute_call({:withdraw, %{:amount => amount}}, %{
          :id => id
        }) do
      {:error, "Can't withdraw money from a suspended account"}
    end

    def execute_call(:close, %{
          :id => id
        }) do
      {:error, "Can't close a suspended account"}
    end

    def execute_call(:suspend, %{:id => id}) do
      {:error, "This account is already suspended"}
    end

    def execute_call(:get_balance, %{:id => id}) do
      %{id: id, balance: 0}
    end
  end

  defp on(state, {:account_opened, %{:customer => customer}}) do
    {Map.put(state, :customer, customer), EsDemo.BankAccounts.AccountWithBehavior.Opened}
  end

  defp on(state, {:money_deposited_to_account, %{:amount => amount}}) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance + amount end)
  end

  defp on(state, {:money_withdrawn_from_account, %{:amount => amount}}) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance - amount end)
  end

  defp on(state, {:account_closed, %{}}) do
    {state, EsDemo.BankAccounts.AccountWithBehavior.Closed}
  end

  defp on(state, {:account_suspended, %{}}) do
    {state, EsDemo.BankAccounts.AccountWithBehavior.Suspended}
  end
end
