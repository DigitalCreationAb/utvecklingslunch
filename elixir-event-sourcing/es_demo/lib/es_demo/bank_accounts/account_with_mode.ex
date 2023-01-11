defmodule EsDemo.BankAccounts.AccountWithMode do
  @moduledoc false

  use Eventize.EventSourcedProcess

  def child_spec(%{id: id} = data) do
    %{
      id: id,
      start: {__MODULE__, :start_link, [data]},
      restart: :transient
    }
  end

  def start_link(%{id: id} = data) do
    GenServer.start_link(
      __MODULE__,
      data,
      name: {:global, id}
    )
  end

  @impl true
  def start(_) do
    %{balance: 0, mode: :not_opened}
  end

  @impl true
  def execute_call({:open, %{customer: customer}}, _from, %{
        id: id,
        state: %{mode: :not_opened}
      }) do
    {[{:account_opened, %{id: id, customer: customer, time_stamp: DateTime.utc_now()}}],
     %{id: id, customer: customer}}
  end

  def execute_call(_command, _from, %{state: %{mode: :not_opened}}) do
    {:error, "This account doesn't exist"}
  end

  def execute_call({:deposit, %{amount: amount}}, _from, %{
        id: id,
        state: %{balance: current_balance, mode: :opened}
      })
      when amount > 0 do
    {[{:money_deposited_to_account, %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}],
     %{id: id, balance: current_balance + amount}}
  end

  def execute_call({:deposit, %{amount: _amount}}, _from, %{state: %{mode: :opened}}) do
    {:error, "Can't deposit negative amount"}
  end

  def execute_call({:withdraw, %{amount: amount}}, _from, %{
        id: id,
        state: %{balance: current_balance, mode: :opened}
      })
      when current_balance >= amount and amount > 0 do
    {[
       {:money_withdrawn_from_account, %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
     ], %{id: id, balance: current_balance - amount}}
  end

  def execute_call({:withdraw, %{amount: amount}}, _from, %{state: %{mode: :opened}})
      when amount <= 0 do
    {:error, "Can't withdraw negative amount"}
  end

  def execute_call({:withdraw, _cmd}, _from, %{state: %{mode: :opened}}) do
    {:error, "Not enough money in account"}
  end

  def execute_call(:close, _from, %{
        id: id,
        state: %{balance: current_balance, mode: :opened}
      })
      when current_balance == 0 do
    {[{:account_closed, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
  end

  def execute_call(:close, _from, %{
        state: %{balance: current_balance, mode: :opened}
      })
      when current_balance > 0 do
    {:error, "Can't close account with money left"}
  end

  def execute_call(:close, _from, %{
        state: %{balance: current_balance, mode: :opened}
      })
      when current_balance < 0 do
    {:error, "Can't close account with outstanding dept"}
  end

  def execute_call(:suspend, _from, %{
        id: id,
        state: %{mode: :opened}
      }) do
    {[{:account_suspended, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
  end

  def execute_call(:get_balance, _from, %{
        id: id,
        state: %{balance: current_balance, mode: :opened}
      }) do
    %{id: id, balance: current_balance}
  end

  def execute_call({:deposit, %{}}, _from, %{state: %{mode: :closed}}) do
    {:error, "Can't deposit money to a closed account"}
  end

  def execute_call({:withdraw, %{}}, _from, %{state: %{mode: :closed}}) do
    {:error, "Can't withdraw money from a closed account"}
  end

  def execute_call(:close, _from, %{state: %{mode: :closed}}) do
    {:error, "This account is already closed"}
  end

  def execute_call(:suspend, _from, %{state: %{mode: :closed}}) do
    {:error, "Can't suspend a closed account"}
  end

  def execute_call(:get_balance, _from, %{id: id, state: %{mode: :closed}}) do
    %{id: id, balance: 0}
  end

  def execute_call(:restore, _from, %{id: id, state: %{mode: :suspended}}) do
    {[{:account_restored, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
  end

  def execute_call({:deposit, %{}}, _from, %{state: %{mode: :suspended}}) do
    {:error, "Can't deposit money into a suspended account"}
  end

  def execute_call({:withdraw, %{}}, _from, %{state: %{mode: :suspended}}) do
    {:error, "Can't withdraw money from a suspended account"}
  end

  def execute_call(:close, _from, %{state: %{mode: :suspended}}) do
    {:error, "Can't close a suspended account"}
  end

  def execute_call(:suspend, _from, %{state: %{mode: :suspended}}) do
    {:error, "This account is already suspended"}
  end

  def execute_call(:get_balance, _from, %{id: id, state: %{mode: :suspended}}) do
    %{id: id, balance: 0}
  end

  @impl true
  def apply_event({:account_opened, %{customer: customer}}, state) do
    Map.put(state, :customer, customer)
    |> Map.put(:mode, :opened)
  end

  def apply_event({:money_deposited_to_account, %{amount: amount}}, state) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance + amount end)
  end

  def apply_event({:money_withdrawn_from_account, %{amount: amount}}, state) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance - amount end)
  end

  def apply_event({:account_closed, _data}, state) do
    Map.put(state, :mode, :closed)
  end

  def apply_event({:account_suspended, _data}, state) do
    Map.put(state, :mode, :suspended)
  end

  def apply_event({:account_restored, _data}, state) do
    Map.put(state, :mode, :opened)
  end

  @impl true
  def cleanup({:account_closed, _data}, _state) do
    :stop
  end
end
