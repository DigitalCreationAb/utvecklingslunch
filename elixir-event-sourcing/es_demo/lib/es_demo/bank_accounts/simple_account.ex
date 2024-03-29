defmodule EsDemo.BankAccounts.SimpleAccount do
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
    %{closed: false, suspended: false, balance: 0}
  end

  @impl true
  def execute_call({:open, %{customer: customer}}, _from, %{
        id: id,
        state: %{closed: closed, suspended: suspended}
      }) do
    case {closed, suspended} do
      {false, false} ->
        {[{:account_opened, %{id: id, customer: customer, time_stamp: DateTime.utc_now()}}],
         %{id: id, customer: customer}}

      {true, _} ->
        {:error, "This is a closed account"}

      {_, true} ->
        {:error, "This is a suspended account"}
    end
  end

  def execute_call({:deposit, %{amount: amount}}, _from, %{
        id: id,
        state: %{balance: current_balance, closed: closed, suspended: suspended}
      }) do
    case {closed, suspended} do
      {false, false} ->
        {[
           {:money_deposited_to_account,
            %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
         ], %{id: id, balance: current_balance + amount}}

      {true, _} ->
        {:error, "Can't deposit money to a closed account"}

      {_, true} ->
        {:error, "Can't deposit money into a suspended account"}
    end
  end

  def execute_call({:withdraw, %{amount: amount}}, _from, %{
        id: id,
        state: %{balance: current_balance, closed: closed, suspended: suspended}
      }) do
    case {current_balance, closed, suspended} do
      {balance, false, false} when balance >= amount ->
        {[
           {:money_withdrawn_from_account,
            %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
         ], %{id: id, balance: current_balance - amount}}

      {_, false, false} ->
        {:error, "Not enough money in account"}

      {_, true, _} ->
        {:error, "Can't withdraw money from a closed account"}

      {_, _, true} ->
        {:error, "Can't withdraw money from a suspended account"}
    end
  end

  def execute_call(:close, _from, %{
        id: id,
        state: %{balance: current_balance, closed: closed, suspended: suspended}
      }) do
    case {current_balance, closed, suspended} do
      {0, false, false} ->
        {[{:account_closed, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}

      {balance, false, false} when balance > 0 ->
        {:error, "Can't close account with money left"}

      {balance, false, false} when balance < 0 ->
        {:error, "Can't close account with outstanding dept"}

      {_, true, _} ->
        {:error, "This account is already closed"}

      {_, _, true} ->
        {:error, "Can't close a suspended account"}
    end
  end

  def execute_call(:suspend, _from, %{
        id: id,
        state: %{closed: closed, suspended: suspended}
      }) do
    case {closed, suspended} do
      {false, false} ->
        {[{:account_suspended, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}

      {true, _} ->
        {:error, "Can't suspend a closed account"}

      {_, true} ->
        {:error, "This account is already suspended"}
    end
  end

  def execute_call(:restore, _from, %{id: id, state: %{suspended: suspended}}) do
    case suspended do
      true -> {[{:account_restored, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
      _ -> {:error, "This account is not suspended"}
    end
  end

  def execute_call(:get_balance, _from, %{id: id, state: %{balance: current_balance}}) do
    %{id: id, balance: current_balance}
  end

  @impl true
  def apply_event({:account_opened, %{customer: customer}}, state) do
    Map.put(state, :customer, customer)
  end

  def apply_event({:money_deposited_to_account, %{amount: amount}}, state) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance + amount end)
  end

  def apply_event({:money_withdrawn_from_account, %{amount: amount}}, state) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance - amount end)
  end

  def apply_event({:account_closed, %{}}, state) do
    Map.put(state, :closed, true)
  end

  def apply_event({:account_suspended, %{}}, state) do
    Map.put(state, :suspended, true)
  end

  def apply_event({:account_restored, %{}}, state) do
    Map.put(state, :suspended, false)
  end
end
