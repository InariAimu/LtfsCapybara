using Ltfs.Index;
using Ltfs.Tasks;

namespace Ltfs;

public partial class Ltfs
{
	private readonly SemaphoreSlim taskExecutionGate = new(1, 1);

	public bool HasPendingTasks => pendingWriteTasks.Any(IsUncommitted) || pendingReadTasks.Any(IsUncommitted) || pendingVerifyTasks.Any(IsUncommitted);

	public async Task<bool> Commit(LtfsTaskQueueType taskQueueType)
	{
		return await ExecuteTaskQueueExclusively(async () =>
		{
			return taskQueueType switch
			{
				LtfsTaskQueueType.Read => await CommitReadTasks(),
				LtfsTaskQueueType.Verify => await CommitVerifyTasks(),
				LtfsTaskQueueType.Write => await CommitWriteTasks(),
				_ => throw new ArgumentOutOfRangeException(nameof(taskQueueType), taskQueueType, null),
			};
		});
	}

	private async Task<bool> CommitWriteTasks()
	{
		var activeTasks = GetUncommittedWriteTasks();
		if (activeTasks.Length == 0)
			return true;

		var executableTasks = activeTasks.Where(IsExecutable).ToArray();
		var workingIndex = (LtfsIndex)GetLatestIndex().Clone();

		foreach (var task in activeTasks)
		{
			UpdateIndexByTask(workingIndex, task);
		}

		foreach (var task in executableTasks.Where(task => task is not WriteTask))
		{
			MarkTaskRunning(task);
			MarkTaskCompleted(task);
		}

		LtfsIndexCurr = workingIndex;

		var writeTasks = executableTasks.OfType<WriteTask>().ToArray();
		if (writeTasks.Length > 0)
		{
			var writeSuccess = await PerformWriteTasks(writeTasks, workingIndex);
			if (!writeSuccess)
				return false;
		}

		WriteLtfsIndex();

		foreach (var task in activeTasks)
		{
			MarkTaskCommitted(task);
		}

		return true;
	}

	private async Task<bool> CommitReadTasks()
	{
		var readTasks = GetUncommittedReadTasks()
			.Where(IsExecutable)
			.Cast<ReadTask>()
			.ToArray();

		return await PerformReadTasks(readTasks, ReadTaskExistingFileMode);
	}

	private async Task<bool> CommitVerifyTasks()
	{
		var verifyTasks = GetUncommittedVerifyTasks()
			.Where(IsExecutable)
			.Cast<VerifyTask>()
			.ToArray();

		return await PerformVerifyTasks(verifyTasks);
	}

	private IReadOnlyList<TaskBase> GetUncommittedTasks()
	{
		return pendingWriteTasks
			.Concat(pendingReadTasks)
			.Concat(pendingVerifyTasks)
			.Where(IsUncommitted)
			.OrderBy(task => task.SequenceNumber)
			.ToArray();
	}

	private TaskBase[] GetUncommittedWriteTasks()
	{
		return pendingWriteTasks
			.Where(IsUncommitted)
			.OrderBy(task => task.SequenceNumber)
			.ToArray();
	}

	private TaskBase[] GetUncommittedReadTasks()
	{
		return pendingReadTasks
			.Where(IsUncommitted)
			.OrderBy(task => task.SequenceNumber)
			.ToArray();
	}

	private TaskBase[] GetUncommittedVerifyTasks()
	{
		return pendingVerifyTasks
			.Where(IsUncommitted)
			.OrderBy(task => task.SequenceNumber)
			.ToArray();
	}

	private async Task<bool> ExecuteTaskQueueExclusively(Func<Task<bool>> action)
	{
		await taskExecutionGate.WaitAsync();
		try
		{
			return await action();
		}
		finally
		{
			taskExecutionGate.Release();
		}
	}

	private static bool IsUncommitted(TaskBase task)
	{
		return task.Status != TaskExecutionStatus.Committed;
	}

	private static bool IsExecutable(TaskBase task)
	{
		return task.Status is TaskExecutionStatus.Pending or TaskExecutionStatus.Failed;
	}

	private static void MarkTaskRunning(TaskBase task)
	{
		task.Status = TaskExecutionStatus.Running;
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;
	}

	private static void MarkTaskCompleted(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Completed;
		task.EndTime = DateTime.UtcNow;
	}

	private static void MarkTaskFailed(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Failed;
		task.EndTime = DateTime.UtcNow;
	}

	private static void MarkTaskCommitted(TaskBase task)
	{
		if (task.StartTime == DateTime.MinValue)
			task.StartTime = DateTime.UtcNow;

		task.Status = TaskExecutionStatus.Committed;
		task.EndTime = DateTime.UtcNow;
	}
}
