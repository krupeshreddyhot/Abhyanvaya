import React, { useEffect, useState } from "react";
import api from "../api/axios";

const Dashboard = () => {
  const [courses, setCourses] = useState([]);

  useEffect(() => {
    const fetchCourses = async () => {
      const res = await api.get("/master/courses");
      setCourses(res.data);
    };

    fetchCourses();
  }, []);

  return (
    <div>
      <h2>Courses</h2>

      <ul>
        {courses.map((c: any) => (
          <li key={c.id}>{c.name}</li>
        ))}
      </ul>
    </div>
  );
};

export default Dashboard;