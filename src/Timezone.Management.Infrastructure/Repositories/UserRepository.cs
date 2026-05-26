using System.Data;
using Dapper;
using Timezone.Management.Domain.Contracts.Repositories;
using Timezone.Management.Domain.Entities;
using Timezone.Management.Domain.Models;
using Timezone.Management.Infrastructure.Database.Contracts;

namespace Timezone.Management.Infrastructure.Repositories;

public class UserRepository(IDbConnectionFactory dbConnectionfactory) : IUserRepository
{
    public async Task<Guid> AddUser(User user)
	{
		using IDbConnection connection = dbConnectionfactory.CreateConnection();
		return await connection.ExecuteScalarAsync<Guid>(
			"""
			INSERT INTO users (name, email)
			VALUES (@Name, @Email)
			RETURNING Uid
			""", user);
	}

	public async Task DeleteUser(Guid userUid)
	{
		using IDbConnection connection = dbConnectionfactory.CreateConnection();
		await connection.ExecuteAsync("DELETE FROM users WHERE uid = @Uid", new { Uid = userUid });
	}

	public async Task<User?> GetUserByUid(Guid userUid)
	{
		using IDbConnection connection = dbConnectionfactory.CreateConnection();
		return await connection.QueryFirstOrDefaultAsync<User>("SELECT uid, name, email FROM users WHERE uid = @Uid", new { Uid = userUid });
	}

	public async Task<IEnumerable<User>> GetUsers(UsersFilter filter)
	{
		using IDbConnection connection = dbConnectionfactory.CreateConnection();

		SqlBuilder builder = new();
		SqlBuilder.Template query = builder.AddTemplate("SELECT uid, name, email FROM users/**WHERE**/");
		
		if (!string.IsNullOrEmpty(filter.Name))
			builder.Where("name = @Name", filter.Name);
		
		if (!string.IsNullOrEmpty(filter.Email))
			builder.Where("email = @Email", filter.Email);
		
		return await connection.QueryAsync<User>(query.RawSql, query.Parameters);
	}

	public async Task UpdateUser(Guid userUid, User user)
	{
		using IDbConnection connection = dbConnectionfactory.CreateConnection();
		await connection.ExecuteAsync("UPDATE users SET name = @Name, email = @Email WHERE uid = @Uid", new { user.Name, user.Email,  Uid = userUid });
	}
}